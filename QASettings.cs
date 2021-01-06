//////////////////////////////////////////////////////////////////////
///Class that read and store the setting for createQAPlan script
///
///--version 1.0.0.3
///Becket Hui 2021/1
///  add phantom isocenter to settings
/// 
///--version 0.0
///Becket Hui 2020/11
//////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.Types;

namespace createQAPlan
{
    public class QASettings
    {
        // Parameters
        public String machId { get; private set; }  // machine ID
        public String pPtId { get; private set; }  // QA phantom patient ID
        public String pImgId { get; private set; }  // QA phantom image ID
        public String pStrutId { get; private set; }  // QA phantom structure ID
        public VVector pIso { get; private set; } // QA phantom iso cetner (convert from cm to mm)
        public Double pLen { get; private set; }  // QA phantom length inferior from iso (convert from cm to mm)
        // Function to read parameters from setting file
        public bool ReadSettings(String settingLine)
        {
            // Read machine ID
            Match m = Regex.Match(settingLine, @"Machine:\s*(.*?)\s*,");
            if (m.Success) { machId = m.Groups[1].Value; }
            else return false;
            // Read QA phantom patient ID
            m = Regex.Match(settingLine, @"Phantom Patient ID:\s*(.*?)\s*,");
            if (m.Success) { pPtId = m.Groups[1].Value; }
            else return false;
            // Read QA phantom image ID
            m = Regex.Match(settingLine, @"Phantom Image ID:\s*(.*?)\s*,");
            if (m.Success) { pImgId = m.Groups[1].Value; }
            else return false;
            // Read QA phantom structure ID
            m = Regex.Match(settingLine, @"Phantom Structure ID:\s*(.*?)\s*,");
            if (m.Success) { pStrutId = m.Groups[1].Value; }
            else return false;
            // Read QA phantom isocenter
            m = Regex.Match(settingLine, @"Phantom IsoCenter\(cm\):\s*(\d*.\d*)\s*,\s*(\d*.\d*)\s*,\s*(\d*.\d*)\s*,");
            if (m.Success) { pIso = new VVector(10.0 * Double.Parse(m.Groups[1].Value), 10.0 * Double.Parse(m.Groups[2].Value), 10.0 * Double.Parse(m.Groups[3].Value)); }
            else pIso = new VVector(0,0,0);
            // Read QA phantom length
            m = Regex.Match(settingLine, @"Phantom Length\(cm\):\s*(\d*.\d*)\s*");
            if (m.Success) { pLen = 10.0 * Double.Parse(m.Groups[1].Value); }
            else pLen = 0;
            // Everything is read successfully
            return true;
        }
    }
}
