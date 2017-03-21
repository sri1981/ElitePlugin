using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.Entities;

namespace Elite.CRM.Plugins
{
    public class TransactionPlugin : BasePlugin
    {
        public TransactionPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_claimtransaction", CreateTransactions);
        }

        protected void CreateTransactions(LocalPluginContext context)
        {
            try
            {
                Entity claimTransation = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                var claim = claimTransation.GetAttributeValue<EntityReference>("new_claim");

                ClaimAttributes attributes = new ClaimAttributes(context.OrganizationService.Retrieve(claim), context.OrganizationService, context.TracingService);

                var claimTransactionType = claimTransation.GetAttributeValue<OptionSetValue>("new_claimtransactiontype"); //attributes.ClaimTransactionType;

                var claimTransactionTypeLabel = context.OrganizationService.GetOptionSetValueLabel("new_claimtransaction", "new_claimtransactiontype", claimTransactionType.Value);

                int transactionType = -1;

                if (claimTransactionTypeLabel == "Reserve Up" || claimTransactionTypeLabel == "Reserve Down")
                {
                    transactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_transaction", "new_type", "Reserve Change");
                }
                else if (claimTransactionTypeLabel == "Recovery Up" || claimTransactionTypeLabel == "Recovery Down" || claimTransactionTypeLabel == "Partial Recovery" || claimTransactionTypeLabel == "Full Recovery")
                {
                    transactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_transaction", "new_type", "Claim Recovery");
                }
                else if (claimTransactionTypeLabel == "Partial Payment" || claimTransactionTypeLabel == "Final Payment")
                {
                    transactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_transaction", "new_type", "Claim Payment");
                }
                else if (claimTransactionTypeLabel == "New Claim")
                {
                    transactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_transaction", "new_type", "Open Claim");
                }
                else if (claimTransactionTypeLabel == "Reopen Claim")
                {
                    transactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_transaction", "new_type", "Reopened Claim");
                }

                Entity transaction = new Entity("new_transaction");
                transaction["new_claim"] = claim;
                transaction["new_claimfolder"] = attributes.ClaimFolder;
                transaction["new_policy"] = attributes.Policy;
                transaction["new_policyid"] = attributes.PolicyVersion;
                transaction["transactioncurrencyid"] = attributes.Currency;
                if (transactionType != -1)
                    transaction["new_type"] = new OptionSetValue(transactionType);
                transaction["new_policyholdercontact"] = attributes.PolicyDetails.PolicyHolderContact;
                transaction["new_policyholdercompany"] = attributes.PolicyDetails.PolicyHolderCompany;
                transaction["new_broker"] = attributes.PolicyDetails.Broker;
                //transaction[""]

                context.OrganizationService.Create(transaction);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }
    }
}
