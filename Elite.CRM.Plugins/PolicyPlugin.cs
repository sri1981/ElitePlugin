using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    public class PolicyPlugin : BasePlugin
    {
        public PolicyPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_policy", PolicyVersionPreCreate);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_policy", PolicyVersionPostCreate);

            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_policy", PolicyVersionPostUpdate);

            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_insuredrisk", CreateInsuredCoversForInsuredRisk);
        }

        #region Main Event handlers

        private void PolicyVersionPreCreate(LocalPluginContext context)
        {
            // performs any transaction-specific value validation or update 
            CheckPolicyVersionValues(context);

            // create policy for New Policy version
            CreatePolicyForNewPolicyVersion(context);
        }

        private void PolicyVersionPostCreate(LocalPluginContext context)
        {
            // create required insured risks if policy version is new/renewal
            CreateInsuredRisksForPolicyVersion(context);

            // Create receipts for policy version 
            CreatePolicyReceipts(context);

            // update policy based on policy version values
            UpdatePolicyBasedOnPolicyVersion(context);

            //update Policy Version with default Excess value and Indemnity
            UpdatePolicyVersionDefaultValues(context);
        }

        private void UpdatePolicyVersionDefaultValues(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, target);
            var excess = policyVersion.Excess;
            var limitOfIndemnity = policyVersion.LimitOfIndemnity;
            var product = policyVersion.Product;

            if(product != null)
            {
                var limitOfIndemnityProduct = product.ProductDefaultLOI;
                var excessProduct = product.ProductDefaultExcess;
                var updatePolicyVersion = new Entity(policyVersion.LogicalName);
                updatePolicyVersion.Id = policyVersion.Id;
                if(limitOfIndemnity == null)
                    updatePolicyVersion["new_limitofindemnity"] = limitOfIndemnityProduct;
                if(excess == null)
                    updatePolicyVersion["new_excess"] = excessProduct;
                context.OrganizationService.Update(updatePolicyVersion);
            }

            
        }

        private void PolicyVersionPostUpdate(LocalPluginContext context)
        {
            // recalculate premiums of policy
            RecalculatePolicyPremiums(context);

            // Update existing policy receipts, if premium changes
            CreatePolicyReceipts(context);
        }

        #endregion

        /// <summary>
        /// Validates policy and modifies field values based on logic for individual transaction types.
        /// </summary>
        /// <param name="context"></param>
        private void CheckPolicyVersionValues(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, target);

            // make sure that policy version contains transaction type 
            if (policyVersion.TransactionType == null)
                throw new InvalidPluginExecutionException("Cannot create Policy Version without Transaction type.");

            if (policyVersion.TransactionType == PolicyVersionTransactionType.Cancellation)
            {
                // cancellations have zero-duration, expiry date is always same as effective date - that is the date
                // of policy cancellation
                target["new_endofcover"] = policyVersion.TransactionEffectiveDate;
            }
        }

        /// <summary>
        /// Creates receipts for a policy version. If receipts already exist, updates existing records.
        /// 
        /// Currently, it assumes one installment and creates only single receipt.
        /// </summary>
        /// <param name="context"></param>
        private void CreatePolicyReceipts(LocalPluginContext context)
        {
            // currently, it creates a single receipt for a policy version
            // TODO implement creation of multiple receipts based on no. of installments
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var postImage = context.PostImage;

            var grossPremium = target.GetAttributeValue<Money>("new_grosspremium");

            // gross premium is not specified/changed, nothing to do.
            if (grossPremium == null || grossPremium.Value == 0)
                return;

            var brokerRef = target.GetAttributeWithFallback<EntityReference>("new_broker", postImage);
            if (brokerRef == null)
                return;

            var transactionEffectiveDate = target.GetAttributeWithFallback<DateTime>("new_commencementofcover", postImage);
            var policyRef = target.GetAttributeWithFallback<EntityReference>("new_policy", postImage);

            // check product, if is sold by (gadget) portal or not
            // portals handle creation of receipts by themselves (or via our web service)
            var productRef = target.GetAttributeWithFallback<EntityReference>("new_productid", postImage);
            if (productRef != null)
            {
                var product = context.OrganizationService.Retrieve(productRef, "new_portalproducttype");
                if (product.GetAttributeValue<OptionSetValue>("new_portalproducttype") != null)
                    return;
            }

            var receipt = new Entity("new_receipt");

            // policy & version
            receipt["new_policy"] = policyRef;
            receipt["new_policyversion"] = target.ToEntityReference();

            // date & amount
            receipt["new_amountdue"] = grossPremium;
            // receipt["new_amountpaid"] = grossPremium;
            receipt["new_duedate"] = transactionEffectiveDate;
            receipt["new_paymentdate"] = transactionEffectiveDate;

            // collected from Broker
            receipt["new_company"] = brokerRef;
            receipt["new_paidby"] = ReceiptPaidBy.Broker.ToOptionSet();

            // method, channel and others
            // receipt["new_paymentstatus"] = ReceiptPaymentStatus.Paid.ToOptionSet();
            receipt["new_paymentchannel"] = ReceiptPaymentChannel.Bordereau.ToOptionSet();
            receipt["new_paymentmethod"] = ReceiptPaymentMethod.BalanceTransfer.ToOptionSet();
            receipt["new_installmentnumber"] = 1;

            // check, if there is an existing receipt. If there is, update existing record.
            var existingReceipt = RetrieveExistingReceipt(context.OrganizationService, target.Id, brokerRef.Id, transactionEffectiveDate);
            if (existingReceipt != null)
            {
                receipt.Id = existingReceipt.Id;
                context.OrganizationService.Update(receipt);
            }
            else
            {
                context.OrganizationService.Create(receipt);
            }
        }

        /// <summary>
        /// Creates a policy folder for new policy version, takes policy version name for a policy and adds '01' to
        /// policy version name making it first version of new policy.
        /// </summary>
        /// <param name="context"></param>
        private void CreatePolicyForNewPolicyVersion(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var transactionType = target.GetAttributeValue<OptionSetValue>("new_transactiontype");
            var transaction = transactionType.ToEnum<PolicyVersionTransactionType>();

            // policy folder is already set, nothing to create
            if (target.Contains("new_policyfolder"))
                return;

            // policy folder has to be created only for new&cancellation transactions
            if (transaction != PolicyVersionTransactionType.NewPolicy &&
                transaction != PolicyVersionTransactionType.Cancellation)
                return;

            // pick up policy number from policy version, pass it to parent policy
            var policyNumber = target.GetAttributeValue<string>("new_name");

            var policies = context.OrganizationService.RetrieveMultipleByName("new_policyfolder", policyNumber);

            if (policies.Any()) // policy folder exists, add as additional policy
            {
                var policy = new Policy(context.OrganizationService, context.TracingService, policies.FirstOrDefault());
                if (!target.Contains("new_policyfolder"))
                    target["new_policy"] = policy.EntityReference;

                target["new_name"] = "{0}{1}".FormatWith(policyNumber, policy.Versions.Count() + 1);
            }
            else // create new policy folder
            {
                var policy = new Entity("new_policyfolder");
                policy["new_name"] = policyNumber;

                // broker
                policy["new_broker"] = target.GetAttributeValue<EntityReference>("new_broker");
                policy["new_brokerpolicyid"] = target.GetAttributeValue<string>("new_brokerpolicyid");

                // policy holder
                policy["new_account"] = target.GetAttributeValue<EntityReference>("new_insuredid");
                policy["new_contact"] = target.GetAttributeValue<EntityReference>("new_insured_contact");

                // dates
                policy["new_inceptiondate"] = target.GetAttributeValue<DateTime?>("new_commencementofcover");
                policy["new_expirydate"] = target.GetAttributeValue<DateTime?>("new_endofcover");

                policy["new_product"] = target.GetAttributeValue<EntityReference>("new_productid");
                policy["new_inputchannel"] = target.GetAttributeValue<OptionSetValue>("new_inputchannel");

                // policy version - policy status
                var policyVersionStatus = target.GetAttributeValue<OptionSetValue>("statuscode").ToEnum<PolicyVersionStatus>();
                var policyStatus = PolicyStatus.OnCover;

                if (policyVersionStatus == PolicyVersionStatus.Quote || policyVersionStatus == PolicyVersionStatus.RequestForQuote)
                    policyStatus = PolicyStatus.Quote;

                policy["statuscode"] = policyStatus.ToOptionSet();

                var policyId = context.OrganizationService.Create(policy);
                var policyRef = new EntityReference("new_policyfolder", policyId);

                var createdPolicy = context.OrganizationService.Retrieve(policyRef);

                // policy version becomes a first version of new policy folder
                target["new_name"] = createdPolicy.GetAttributeValue<string>("new_name") + "01";
                target["new_policy"] = policyRef;
            }
        }

        /// <summary>
        /// Performs updates of policy folder based on policy version.
        /// </summary>
        /// <param name="context"></param>
        private void UpdatePolicyBasedOnPolicyVersion(LocalPluginContext context)
        {
            var postImage = context.PostImage;
            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, postImage);

            var transaction = policyVersion.TransactionType;
            var policyFolder = policyVersion.PolicyFolder;

            if (policyFolder == null)
                return;

            var policyFolderToUpdate = new Entity(policyFolder.LogicalName) { Id = policyFolder.Id };
            policyFolderToUpdate["new_expirydate"] = policyVersion.TransactionExpiryDate;

            // If policy version is a cancellation, cancel policy folder as well
            if (transaction == PolicyVersionTransactionType.Cancellation)
            {
                var cancellationDate = policyVersion.TransactionEffectiveDate;

                // cancellation type
                // TODO grace period based on product
                if (cancellationDate == policyFolder.InceptionDate)
                    policyFolderToUpdate["new_cancellationtype"] = PolicyCancellationType.FromInception.ToOptionSet();
                else if (cancellationDate == policyFolder.ExpiryDate)
                    policyFolderToUpdate["new_cancellationtype"] = PolicyCancellationType.OnExpiry.ToOptionSet();
                else
                    policyFolderToUpdate["new_cancellationtype"] = PolicyCancellationType.HalfWayCancellation.ToOptionSet();

                // cancellation dates
                if (policyFolder.CancellationRequestDate == null)
                    policyFolderToUpdate["new_cancellationrequestdate"] = cancellationDate;

                policyFolderToUpdate["new_datecancellation"] = cancellationDate;

                // set status to cancelled
                var stateRequest = new SetStateRequest()
                {
                    EntityMoniker = policyFolder.EntityReference,
                    State = CustomEntityStatus.Active.ToOptionSet(),
                    Status = PolicyStatus.Cancelled.ToOptionSet()
                };

                context.OrganizationService.Execute(stateRequest);
            }

            context.OrganizationService.Update(policyFolderToUpdate);
        }

        private void RecalculatePolicyPremiums(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var postImage = context.PostImage;
            
            // trigger for recalculation
            if (!target.Contains("new_recalc") || !target.GetAttributeValue<bool>("new_recalc"))
                return;

            // reset the recalc flag
            target["new_recalc"] = false;

            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, postImage);
            var product = policyVersion.Product;

            var premiumCalculator = new PremiumCalculation.PolicyPremiumCalculator(context.OrganizationService, product, policyVersion);

            // base and gross premiums
            premiumCalculator.CalculateBasePremium();
            premiumCalculator.CalculateRiskFactors();
            premiumCalculator.CalculateGrossPremium();

            // tax
            premiumCalculator.CalculateTax();

            // commissions 
            premiumCalculator.CalculateCommission();

            // reinsurance
            premiumCalculator.CalculateReinsurance();

            // update records
            premiumCalculator.UpdatePremiums();
        }

        /// <summary>
        /// Creates insured risks for new/renewal policy version
        /// </summary>
        /// <param name="context"></param>
        private void CreateInsuredRisksForPolicyVersion(LocalPluginContext context)
        {
            var postImage = context.PostImage;
            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, postImage);

            // check if policy needs to be initialized with all insured risks and covers 
            if (!CheckIfCreatingInsuredRisks(policyVersion))
                return;

            var product = policyVersion.Product;
            var firstCover = product.Covers.FirstOrDefault();

            foreach (var risk in product.Risks)
            {
                // create only risk objects with minimum > 0
                if (risk.MinimumNumberOfRisks <= 0)
                    continue;

                var insuredRisk = new Entity("new_insuredrisk");
                insuredRisk["new_product"] = product.EntityReference;
                insuredRisk["new_policyid"] = policyVersion.EntityReference;

                if (risk.RiskClassRef != null)
                    insuredRisk["new_riskclassid"] = risk.RiskClassRef;

                if (risk.RiskSubClassRef != null)
                    insuredRisk["new_secondlevelriskclass"] = risk.RiskSubClassRef;

                insuredRisk["new_riskid"] = risk.EntityReference;

                context.OrganizationService.Create(insuredRisk);
            }

            // trigger recalculation of totals on policy version
            //var triggerPolicy = new Entity(postImage.LogicalName) { Id = postImage.Id };
            //triggerPolicy["new_recalc"] = true;
            //context.OrganizationService.Update(triggerPolicy);
        }

        private void CreateInsuredCoversForInsuredRisk(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var insuredRisk = new InsuredRisk(context.OrganizationService, context.TracingService, target);
            var excess = insuredRisk.RiskExcess;
            var linitOfIndemnity = insuredRisk.RiskLOI;

            var retrievedRisk = insuredRisk.Risk;

            if (retrievedRisk != null)
            {
                var riskExcess = retrievedRisk.RiskDefaultExcess;
                var riskLOI = retrievedRisk.RiskDefaultLOI;

                Entity insuredRiskToUpdate = new Entity(insuredRisk.LogicalName);
                insuredRiskToUpdate.Id = insuredRisk.Id;
                if(riskExcess == null)
                    insuredRiskToUpdate["new_excess"] = riskExcess;
                if(riskLOI == null)
                    insuredRiskToUpdate["new_limitofindemnity"] = riskLOI;

                context.OrganizationService.Update(insuredRiskToUpdate);
            }
            
            if (!CheckIfCreatingInsuredRisks(insuredRisk.PolicyVersion))
                return;

            //if (!CheckIfCreatingInsuredRisks(insuredRisk.PolicyVersion))
            //    return;

            if (!CheckIfCreatingInsuredRisks(insuredRisk.PolicyVersion))
                return;

            var risk = insuredRisk.Risk;
            if (risk == null)
                throw new InvalidPluginExecutionException("Insured risk requires a risk ID.");

            foreach (var c in risk.Covers)
            {
                if (!c.Mandatory)
                    continue;

                var newInsCover = new Entity("new_insuredcover");
                newInsCover["new_policyid"] = insuredRisk.PolicyVersionRef;
                newInsCover["new_insuredriskid"] = insuredRisk.EntityReference;
                newInsCover["new_coverid"] = c.EntityReference;
                context.OrganizationService.Create(newInsCover);
            }
        }

        /// <summary>
        /// Checks if policy version needs to get insured risks created.
        /// </summary>
        /// <param name="policyVersion"></param>
        /// <returns></returns>
        private bool CheckIfCreatingInsuredRisks(PolicyVersion policyVersion)
        {
            ThrowIf.Argument.IsNull(policyVersion, "policyVersion");

            // all initialization of policies created by portal is managed by portal itself
            if (policyVersion.InputChannel == PolicyInputChannel.Portal)
                return false;

            // all new policies and renewals need to get insured risks created
            if (policyVersion.TransactionType == PolicyVersionTransactionType.NewPolicy ||
                policyVersion.TransactionType == PolicyVersionTransactionType.Renewal)
                return true;

            // cancellations for Bordereau are a special case, which needs same initialization as in new policy versions
            if (policyVersion.TransactionType == PolicyVersionTransactionType.Cancellation &&
                policyVersion.InputChannel == PolicyInputChannel.Import)
                return true;

            return false;
        }

        private Entity RetrieveExistingReceipt(IOrganizationService svc, Guid policyVersionId, Guid brokerId, DateTime paymentDueDate)
        {
            var receiptQuery = new QueryExpression("new_receipt");

            receiptQuery.Criteria.AddCondition("new_policyversion", ConditionOperator.Equal, policyVersionId);
            receiptQuery.Criteria.AddCondition("new_company", ConditionOperator.Equal, brokerId);
            receiptQuery.Criteria.AddCondition("new_duedate", ConditionOperator.Equal, paymentDueDate);

            return svc.RetrieveMultiple(receiptQuery).Entities.FirstOrDefault();
        }

    }
}
