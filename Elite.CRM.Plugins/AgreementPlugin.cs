using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.Entities;
using Microsoft.Crm.Sdk.Messages;

namespace Elite.CRM.Plugins
{
    //Comments by Sri Iyer on 16/02/2017
    //This plugin is a like for like from the code that was written by PIAS. No changes have been made apart from re-factoring the code for better understanding.
    public class AgreementPlugin : BasePlugin
    {
        public AgreementPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new__agreement", AgreementCreate);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new__agreement", AgreementUpdate);
        }

        public enum AgreementStage
        {
            Draft = 1,
            SubmittedForApproval = 100000000,
            Approved = 100000001,
            Sent = 100000002,
            Received = 100000003,
            Active = 100000004,
            Expired = 2,
            Cancelled = 100000005,
            Rejected = 100000006,
            Lost = 100000007
        }

        private void AgreementUpdate(LocalPluginContext context)
        {
            var svc = context.OrganizationService;
            var agreement = new Agreement(svc, context.PluginExecutionContext.InputParameters["Target"] as Entity);
            var preAgreement = new Agreement(svc, context.PreImage);

            if(agreement.StatusCode != null && (AgreementStage) agreement.StatusCode.Value == AgreementStage.SubmittedForApproval)
            {
                agreement.AssignAgreement(svc, agreement, preAgreement);
            }
            else if(agreement.EstimatedAnnualRevenue != null)
            {
                var statusCode = preAgreement.StatusCode;
                agreement.AssignAgreement(svc, agreement, preAgreement);
            }
        }

        private void AgreementCreate(LocalPluginContext context)
        {
            var svc = context.OrganizationService;
            var agreement = new Agreement(svc, context.PluginExecutionContext.InputParameters["Target"] as Entity);

            if(agreement.StatusCode != null && (AgreementStage) agreement.StatusCode.Value == AgreementStage.SubmittedForApproval)
            {
                agreement.AssignAgreement(svc, agreement, agreement);
            }
        }

       
    }
}
