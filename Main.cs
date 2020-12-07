//////////////////////////////////////////////////////////////////////
///This project creates and export the QA verification plan for the current patient//
///--version 0.0
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

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
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
