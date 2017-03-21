using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    class __PluginTemplate : BasePlugin
    {
        public __PluginTemplate(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "[EntityLogicalName]", PluginMethod);
        }

        protected void PluginMethod(LocalPluginContext context)
        {

        }
    }
}
