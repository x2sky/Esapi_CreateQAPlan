using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;

[assembly: AssemblyVersion("1.0.0.1")]

[assembly: ESAPIScript(IsWriteable = false)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            String msgTxt = "";
            Patient currPt = context.Patient;
            Course currCrs = context.Course;
            ExternalPlanSetup currPln = context.ExternalPlanSetup;
            String assemblyFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            msgTxt = assemblyFolder;
            MessageBox.Show(msgTxt);

            msgTxt = "Done";
            MessageBox.Show(msgTxt);
        }
    }
}