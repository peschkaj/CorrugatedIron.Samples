using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Sample.SessionStateProvider
{
    class RiakSessionStateConfiguration : ConfigurationSection
    {
        public static RiakSessionStateConfiguration LoadFromConfig(string sectionName)
        {
            return (RiakSessionStateConfiguration)ConfigurationManager.GetSection(sectionName);
        }

        [ConfigurationProperty("timeout_in_ms", IsDefaultCollection = true, IsRequired = true)]
        public int TimeoutInMilliseconds
        {
            get { return (int) this["timeout_in_ms"]; }
            set { this["timeout_in_ms"] = value;  }
        }
    }
}
