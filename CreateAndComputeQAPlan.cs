//////////////////////////////////////////////////////////////////////
///Class and functions to create and compute verification plan
/// Include functions:
///     CreateAndComputeQAPlan(Patient, ExternalPlanSetup) -- class initiation
///     CreateCourse(CrsIDString) -- create QA course
///     CopyImageSet(QASettings) -- copy phantom image and structure to the current patient
///     CreateVerificationPlan(StructureSet, Course, plnIDString) -- create verification plan that links to current plan
///     ComputeIsoShift(QASettings)  -- compute the inferior field edge and evaluate if iso center needs to be shift to avoid phantom's electronic
///     AddBeamToVerifPlan(Beam)  -- copy a beam from current plan to the verification plan 
///     ComputeDose()  -- compute dose in the verification plan
///     getMLCBmTechnique(Beam)  -- get the beam delivery & MLC type
///     copyControlPoints(Beam, Beam)  -- copy leaf and jaw positions from beam in current plan to beam in verification plan
///     GetMedian(srclist)  -- compute median from a list
///
///--version 1.0.0.2
///Becket Hui 2020/12
///  Change AddBeamToVerifPlan to handle fluence beam such as SRS and FFF
///  
///--version 1.0.0.1
///Becket Hui 2020/12
//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace createQAPlan
{
    class CreateAndComputeQAPlan
    {
        private Patient currPt;
        private ExternalPlanSetup currPln;
        private ExternalPlanSetup verifPln;
        private VVector verifPlnIso;
        List<KeyValuePair<string, MetersetValue>> muValues = new List<KeyValuePair<string, MetersetValue>>();
        private String bmTech;
        public CreateAndComputeQAPlan(Patient pt, ExternalPlanSetup pln)
        {
            currPt = pt;
            currPln = pln;
        }
        // Create course
        public Course CreateCourse(String verifCrsID)
        {
            Course verifCrs = currPt.AddCourse();
            verifCrs.Id = verifCrsID;
            return verifCrs;
        }
        // Copy QA phantom struct set if not available
        public StructureSet CopyImageSet(QASettings qaSet)
        {
            return currPt.CopyImageFromOtherPatient(qaSet.pPtId, null, qaSet.pImgId);
        }
        // Create verification plan
        public ExternalPlanSetup CreateVerificationPlan(StructureSet pStructSt, Course verifCrs, String verifPlnID)
        {
            verifPln = verifCrs.AddExternalPlanSetupAsVerificationPlan(pStructSt, currPln);
            verifPln.Id = verifPlnID;
            // Copy and set prescription info//
            verifPln.SetPrescription(1, currPln.DosePerFraction, currPln.TreatmentPercentage);
            // Set Iso Center
            verifPlnIso = verifPln.StructureSet.Image.UserOrigin;
            return verifPln;
        }
        // Compute isocenter shift on verification plan
        public VVector ComputeIsoShift(QASettings qaSet)
        {
            List<Double> infFldEdges = new List<Double>();  // create list to store inferior field edge for each beam
            foreach (Beam bm in currPln.Beams)
            {
                if (bm.MetersetPerGy > 0)
                {
                    BeamParameters bmParam = bm.GetEditableParameters();
                    Double collAng = bmParam.ControlPoints.First().CollimatorAngle;
                    VRect<Double> collPos = bmParam.ControlPoints.First().JawPositions;
                    // Compute the approximate inferior field edge position shaped by the collimator and MLC
                    Double cosAng = Math.Cos(Math.PI * collAng / 180.0);
                    Double sinAng = Math.Sin(Math.PI * collAng / 180.0);
                    Double DelX = 400.0;  //field edge can be defined by one X and one Y jaw
                    Double DelY = 400.0;
                    if (cosAng > 0.01) { DelY = -collPos.Y1 / cosAng; }
                    if (cosAng < -0.01) { DelY = -collPos.Y2 / cosAng; } //past 90 deg, Y2 defines inferior edge
                    if (sinAng > 0.01) { DelX = -collPos.X1 / sinAng; }  //ccw rotation (respect to BEV), X1 defines inferior edge
                    if (sinAng < -0.01) { DelX = -collPos.X2 / sinAng; } //cw rotation (respect to BEV), X2 defines inferior edge
                    infFldEdges.Add(Math.Min(DelX, DelY)); //use the minimum between DelX and DelY as the approximate inferior field edge
                }
            }
            if (infFldEdges.Count == 0) { return new VVector(Double.NaN, Double.NaN, Double.NaN); } // no field edge found, either beam parameters invalid or no MU
            Double MedInfFldEdge = GetMedian(infFldEdges);
            if (MedInfFldEdge > qaSet.pLen)  // if the field edge is longer than the length of qa phantom
            {
                verifPlnIso.z = verifPlnIso.z + Math.Ceiling(MedInfFldEdge / 10 - qaSet.pLen / 10) * 10;  // shift the iso superiorly (cm as minimum shift unit)
            }
            return verifPlnIso;
        }
        // Add beam to the plan
        public Beam AddBeamToVerifPlan(Beam currBm)
        {
            if (verifPln == null)
            {
                return null;
            }
            else
            {
                bmTech = getMLCBmTechnique(currBm);  // find beam technique
                // Create machine parameters
                String energy = currBm.EnergyModeDisplayName;
                String fluence = null;
                Match EMode = Regex.Match(currBm.EnergyModeDisplayName, @"^([0-9]+[A-Z]+)-?([A-Z]+)?", RegexOptions.IgnoreCase);  //format is... e.g. 6X(-FFF)
                if (EMode.Success)
                {
                    if (EMode.Groups[2].Length > 0)  // fluence mode
                    {
                        energy = EMode.Groups[1].Value;
                        fluence = EMode.Groups[2].Value;
                    } // else normal modes uses default in decleration
                }
                ExternalBeamMachineParameters machParam = new ExternalBeamMachineParameters(currBm.TreatmentUnit.Id.ToString(), 
                    energy, currBm.DoseRate, currBm.Technique.Id.ToString(), fluence);
                // Define collimator, gantry and couch angles //
                Double gantryAng = currBm.ControlPoints.First().GantryAngle;
                Double collAng = currBm.ControlPoints.First().CollimatorAngle;
                Double couchAng = 0.0;
                // MU values for each control point //
                IEnumerable<double> muSet = currBm.ControlPoints.Select(cp => cp.MetersetWeight).ToList();
                // Add beam MU to the list of MU values //
                muValues.Add(new KeyValuePair<string, MetersetValue>(currBm.Id, currBm.Meterset));
                // Start adding beam based on beam technique //
                if (bmTech == "StaticMLC")
                {
                    Beam verifBm = verifPln.AddMLCBeam(machParam, new float[2, 60], new VRect<double>(-10.0, -10.0, 10.0, 10.0),
                        collAng, gantryAng, couchAng, verifPlnIso);
                    verifBm.Id = currBm.Id;
                    BeamParameters ctrPtParam = copyControlPoints(currBm, verifBm); 
                    verifBm.ApplyParameters(ctrPtParam);
                    return verifBm;
                }
                else if (bmTech == "StaticSegWin")
                {
                    Beam verifBm = verifPln.AddMultipleStaticSegmentBeam(machParam, muSet,
                        collAng, gantryAng, couchAng, verifPlnIso);
                    verifBm.Id = currBm.Id;
                    BeamParameters ctrPtParam = copyControlPoints(currBm, verifBm);
                    verifBm.ApplyParameters(ctrPtParam);
                    return verifBm;
                }
                else if (bmTech == "StaticSlidingWin")
                {
                    Beam verifBm = verifPln.AddSlidingWindowBeam(machParam, muSet,
                        collAng, gantryAng, couchAng, verifPlnIso);
                    verifBm.Id = currBm.Id;
                    BeamParameters ctrPtParam = copyControlPoints(currBm, verifBm);
                    verifBm.ApplyParameters(ctrPtParam);
                    return verifBm;
                }
                else if (bmTech == "ConformalArc")
                {
                    Beam verifBm = verifPln.AddConformalArcBeam(machParam, collAng, currBm.ControlPoints.Count(),
                        currBm.ControlPoints.First().GantryAngle, currBm.ControlPoints.Last().GantryAngle,
                        currBm.GantryDirection, couchAng, verifPlnIso);
                    verifBm.Id = currBm.Id;
                    BeamParameters ctrPtParam = copyControlPoints(currBm, verifBm);
                    verifBm.ApplyParameters(ctrPtParam);
                    return verifBm;
                }
                else if (bmTech == "VMAT")
                {
                    Beam verifBm = verifPln.AddVMATBeam(machParam, muSet, collAng,
                        currBm.ControlPoints.First().GantryAngle, currBm.ControlPoints.Last().GantryAngle,
                        currBm.GantryDirection, couchAng, verifPlnIso);
                    verifBm.Id = currBm.Id;
                    BeamParameters ctrPtParam = copyControlPoints(currBm, verifBm);
                    verifBm.ApplyParameters(ctrPtParam);
                    return verifBm;
                }
                else // null
                {
                    return null;
                }
            }
        }
        // Compute dose
        public void ComputeDose()
        {
            // Set normalization value
            verifPln.PlanNormalizationValue = currPln.PlanNormalizationValue;
            // Set the plan calculation model
            verifPln.SetCalculationModel(CalculationType.PhotonVolumeDose, currPln.PhotonCalculationModel);
            verifPln.CalculateDoseWithPresetValues(muValues);  // Compute dose
        }
        // Determine the add beam method based on beam technique and mlc technique//
        private string getMLCBmTechnique(Beam bm)
        {
            if (bm.Technique.Id.ToString() == "STATIC" && bm.MLCPlanType == MLCPlanType.Static)
            {
                return "StaticMLC";
            }
            else if (bm.Technique.Id.ToString() == "STATIC" && bm.MLCPlanType == MLCPlanType.DoseDynamic)
            {
                // Check if the MLC technique is Sliding Window or Segmental
                var lines = bm.CalculationLogs.FirstOrDefault(log => log.Category == "LMC");
                foreach (var line in lines.MessageLines)
                {
                    if (line.ToUpper().Contains("MULTIPLE STATIC SEGMENTS")) { return "StaticSegWin"; }
                    if (line.ToUpper().Contains("SLIDING WINDOW")) { return "StaticSlidingWin"; }
                    if (line.ToUpper().Contains("SLIDING-WINDOW")) { return "StaticSlidingWin"; }
                }
                return null;
            }
            else if (bm.Technique.Id.ToString() == "ARC" && bm.MLCPlanType == MLCPlanType.ArcDynamic)
            {
                return "ConformalArc";
            }
            else if (bm.Technique.Id.ToString() == "ARC" && bm.MLCPlanType == MLCPlanType.VMAT)
            {
                return "VMAT";
            }
            else
            {
                return null;
            }
        }
        // Copy the MLC and jaw positions from the beam in approved plan to the beam in verificiation plan
        private BeamParameters copyControlPoints(Beam currBm, Beam verifBm)
        {
            BeamParameters verifBmParam = verifBm.GetEditableParameters();
            for (int i_CP = 0; i_CP < verifBm.ControlPoints.Count(); i_CP++)
            {
                verifBmParam.ControlPoints.ElementAt(i_CP).LeafPositions = currBm.ControlPoints.ElementAt(i_CP).LeafPositions;
                verifBmParam.ControlPoints.ElementAt(i_CP).JawPositions = currBm.ControlPoints.ElementAt(i_CP).JawPositions;
            }
            verifBmParam.WeightFactor = currBm.WeightFactor;
            return verifBmParam;
        }
        // Get median value in a list //
        public static Double GetMedian(IEnumerable<Double> srcList)
        {
            // Create a copy of the input, and sort the copy
            Double[] tmpList = srcList.ToArray();
            Array.Sort(tmpList);
            int count = tmpList.Length;
            if (count == 0)
            {
                throw new InvalidOperationException("List input to GetMedian is empty.");
            }
            else if (count % 2 == 0)
            {
                // count is even, average two middle elements
                Double a = tmpList[count / 2 - 1];
                Double b = tmpList[count / 2];
                return (a + b) / 2.0;
            }
            else
            {
                // count is odd, return the middle element
                return tmpList[count / 2];
            }
        }
    }
}
