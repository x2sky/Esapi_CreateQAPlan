//////////////////////////////////////////////////////////////////////
///Main window widget for createQAPlan script//
///--version 0.0
///Becket Hui 2020/11
//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace createQAPlan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UserControl
    {
        private ScriptContext EclipseContext;
        private Patient currPt;
        private Course currCrs;
        private ExternalPlanSetup currPln;
        public List<QASettings> qaSetLs;
        public String msgTxt;
        public MainWindow(ScriptContext context, Patient pt, List<QASettings> ls)
        {
            InitializeComponent();
            EclipseContext = context;
            currPt = pt;
            qaSetLs = ls;
            fillPtInfo();
        }
        // Fill in patient & plan information to the MainWindow
        private void fillPtInfo()
        {
            // Fill in patient info
            lblPtName.Content = currPt.Name;
            lblPtMRN.Content = currPt.Id;
            // Fill in current course
            currCrs = EclipseContext.Course;
            lblCrs.Content = currCrs.Id;
            // Create list of all plans with course
            foreach (ExternalPlanSetup pln in currCrs.PlanSetups)
            {
                cmbPln.Items.Add(pln.Id);
            }
            // Select the current plan as default plan in the plan list
            currPln = EclipseContext.ExternalPlanSetup;
            if (currPln != null)
            {
                cmbPln.SelectedValue = currPln.Id;
            }
            // Create the default course name for the QA plan course
            Match m = Regex.Match(currCrs.Id, @"C\s*[0-9]+", RegexOptions.IgnoreCase);
            if (m.Success && m.Length < 10)
            {
                txtbQACrs.Text = m.Value + ": QA";
            }
        }
        // Create QA button is clicked
        private void btnCrtQA_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected plan
            currPln = EclipseContext.Course.ExternalPlanSetups.FirstOrDefault(p => p.Id == cmbPln.SelectedItem.ToString());
            // Get the name of the QA course
            String verifCrsID = txtbQACrs.Text;
            // Create a new CreateAndComputeQAPlan Object
            CreateAndComputeQAPlan CrtQApln = new CreateAndComputeQAPlan(currPt, currPln);
            // Check and/or create QA course
            Course verifCrs = currPt.Courses.FirstOrDefault(c => c.Id == verifCrsID);
            if (verifCrs == null)
            {
                msgTxt = "Creating course " + verifCrsID + "...";
                ShowMessage(msgTxt);
                verifCrs = CrtQApln.CreateCourse(verifCrsID);
            }
            // Check if current plan already exists in verifCrs
            String verifPlnID = currPln.Id;
            if (verifCrs.PlanSetups.Any(p => p.Id == verifPlnID))
            {
                msgTxt = "Plan " + verifPlnID + " already exists in course " + verifCrsID + ".";
                ShowMessage(msgTxt);
                return;
            }
            // Copy QA phantom struct set if not available
            QASettings qaSet = FindQASetbyMachine(); // find the qa setting of the machine of the current plan
            if (qaSet == null)
            {
                msgTxt = "Treatment machine is not set in settings or multiple machines present in plan!";
                ShowMessage(msgTxt);
                return;
            }
            StructureSet pStructSt = currPt.StructureSets.FirstOrDefault(s => s.Id == qaSet.pStrutId);
            if (pStructSt == null)
            {
                msgTxt = "Copying structure set " + qaSet.pStrutId + " from patient " + qaSet.pPtId + "...";
                ShowMessage(msgTxt);
                pStructSt = currPt.CopyImageFromOtherPatient(qaSet.pPtId, null, qaSet.pImgId);
            }
            // Create verification plan
            msgTxt = "Creating QA plan " + verifPlnID + "...";
            ShowMessage(msgTxt);
            ExternalPlanSetup verifPln = CrtQApln.CreateVerificationPlan(pStructSt, verifCrs, verifPlnID);
            // Compute if isocenter shift is needed based on QA phantom length
            VVector verifPlnIso = CrtQApln.ComputeIsoShift(qaSet);  //  the isocenter location of the QA plan
            if (Double.IsNaN(verifPlnIso.z))
            {
                msgTxt = "Cannot find field edge.";
                ShowMessage(msgTxt);
                msgTxt = "Please double check field parameters and its MU.";
                ShowMessage(msgTxt);
                return;
            }
            int shftIso = (int)(verifPlnIso.z / 10 - verifPln.StructureSet.Image.UserOrigin.z / 10);  // convert iso shift to cm
            if (shftIso > 0)
            {
                msgTxt = "Iso-center of the QA plan will be shifted by " + shftIso.ToString() + "cm superiorly.";
                ShowMessage(msgTxt);
            }
            // Create beams in verification plan
            foreach (Beam currBm in currPln.Beams)
            {
                if (currBm.MetersetPerGy > 0)
                {
                    if (currBm.ControlPoints.First().PatientSupportAngle != 0.0)
                    {
                        msgTxt = "Couch angle for beam " + currBm.Id + " will be set to 0.0.";
                        ShowMessage(msgTxt);
                    }
                    msgTxt = "Adding beam " + currBm.Id + " to QA plan " + verifPln.Id + "...";
                    ShowMessage(msgTxt);
                    Beam verifBm = CrtQApln.AddBeamToVerifPlan(currBm);
                    if (verifBm == null)
                    {
                        msgTxt = "Cannot add beam " + currBm.Id + " to QA plan, please delete QA plan & try again.";
                        ShowMessage(msgTxt);
                        return;
                    }
                }
            }
            // Compute dose in verification plan
            msgTxt = "Calculating dose for QA plan " + verifPln.Id + "...";
            ShowMessage(msgTxt);
            CrtQApln.ComputeDose();
            // Ready for next
            msgTxt = "Dose calculation completed.";
            ShowMessage(msgTxt);
            if (shftIso > 0)
            {
                msgTxt = "Please inform QA personnel:";
                ShowMessage(msgTxt);
                msgTxt = "Iso-center of QA plan is shifted by " + shftIso.ToString() + "cm.";
                ShowMessage(msgTxt);
            }
            txtbStat.Text = "Ready.";
        }
        // Exit button
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
        // Write message to list view and status bar
        private void ShowMessage(String msg)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                lstvHst.Items.Add(msg);  // Add message to list view history
                // Get scrollviewer and make it scroll to bottom
                Decorator bdrHst = VisualTreeHelper.GetChild(lstvHst, 0) as Decorator;
                ScrollViewer svrHst = bdrHst.Child as ScrollViewer;
                svrHst.ScrollToBottom();
            }));
            // Make list view history update using a new thread
            lstvHst.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
            txtbStat.Text = msg;
            // Make status bar update using a new thread
            txtbStat.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
        }
        // Find QA setting based on machine of the current selected plan //
        private QASettings FindQASetbyMachine()
        {
            String Machine = "None";
            foreach (Beam bm in currPln.Beams)
            {
                if (Machine == "None")
                {
                    Machine = bm.TreatmentUnit.Id.ToString();
                }
                else
                {
                    if (Machine != bm.TreatmentUnit.Id.ToString())  // If different beam uses different machine, exit //
                    {
                        return null;
                    }
                }
            }
            return qaSetLs.FirstOrDefault(s => s.machId == Machine);
        }
    }
}
