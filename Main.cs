//////////////////////////////////////////////////////////////////////
///This program creates and export the QA verification plan for the current patient//
///The program contains a user control window with options to choose course and plan to perform QA.
///It reads from a setting file to handle multiple machines and their corres. QA phantoms, the setting file "createQAPlan.setting" is placed in the same folder as the dll.
///To create the QA plan, the process involves copying the QA phantom image & structure set; creating QA course & verification plan; computing isocenter shift;
///copying beams and computing the verification plan.
///
///--version 1.0.0.4
///Becket Hui 2021/1
///  Add SRS Arc and SRS Static beam techniques
/// 
///--version 1.0.0.3
///Becket Hui 2021/1
///  Change to v16, add code to remove target struct, add code to apply calculation options, add phantom isocenter to setting,
///  add code to correct beam MU for plan with non-IMRT beams, add color option to list view history
/// 
///--version 1.0.0.2
///Becket Hui 2020/12
///  Change CreateAndComputeQAPlan to handle fluence beam such as SRS and FFF
///  
///--version 1.0.0.1
///Becket Hui 2020/11
//////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.IO;
using createQAPlan;
using System.Diagnostics;
using System.Text.RegularExpressions;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.4")]
[assembly: AssemblyFileVersion("1.0.0.4")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, Window MainWin)
        {
            // Check version of the ESAPI dll
            String esapiVer = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(ExternalPlanSetup)).Location).FileVersion;
            Match mEsapiVer = Regex.Match(esapiVer, @"^(\d+).");
            if (mEsapiVer.Success)
            {
                int esapiVer0 = Int32.Parse(mEsapiVer.Groups[1].Value);
                if (esapiVer0 < 16)
                    throw new ApplicationException("ESAPI ver." + esapiVer + ", script cannot run on version below 16.");
            }

            // Open current patient
            Patient currPt = context.Patient;
            // If there's no selected patient, throw an exception
            if (currPt == null)
            throw new ApplicationException("Please open a patient before using this script.");
            currPt.BeginModifications();

            // Open current course
            Course currCrs = context.Course;
            // If there's no selected course, throw an exception
            if (currCrs == null)
            throw new ApplicationException("Please select at least one course before using this script.");

            // Read setting file
            String locPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);  // path of the compiled dll file
            String setFilePath = Path.Combine(locPath, @"createQAPlan.setting");
            List<QASettings> qaSetLs = new List<QASettings>();
            if (File.Exists(setFilePath))
            {
                try
                {
                    using (StreamReader readr = new StreamReader(setFilePath))
                    {
                        String currLine;
                        while ((currLine = readr.ReadLine()) != null)
                        {
                            QASettings qaSet = new QASettings();
                            bool succ = qaSet.ReadSettings(currLine);
                            if (succ) { qaSetLs.Add(qaSet); }
                            else
                            {
                                throw new ApplicationException("Something wrong in createQAPlan.setting, please check the file before using this script");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    throw new ApplicationException("Error in reading createQAPlan.setting, please check the file before using this script");
                }
            }
            else
            {
                throw new ApplicationException("Cannot locate createQAPlan.setting, please check the file before using this script");
            }

            // Call WPF Win
            var MainWinCtr = new createQAPlan.MainWindow(context, currPt, qaSetLs);
            MainWin.Content = MainWinCtr;
            MainWin.Title = "Create QA Plan";
            MainWin.Width = 515;
            MainWin.Height = 342;
            MainWin.ResizeMode = ResizeMode.NoResize;
        }
    }
}
