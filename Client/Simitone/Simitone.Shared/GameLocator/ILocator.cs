using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Simitone.Windows.GameLocator
{
    public interface ILocator
    {
        string FindTheSimsOnline();
        string FindTheSims1();
        
        /// <summary>
        /// Gets all detected The Sims 1 installations
        /// </summary>
        List<(string description, string path, TS1InstallationType type)> GetAllTheSims1Installations();
    }
}
