using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Elite.CRM.Plugins.BordereauImport;
using Elite.CRM.Plugins.Entities;
using System.Text.RegularExpressions;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    public class AutoNumbering : BasePlugin
    {
        public AutoNumbering(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            // claim-related auto names/numbers
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_claim", AutoNumberClaimOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_payment", AutoNumberClaimPaymentOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_claimtransaction", AutoNumberClaimtransaction);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_claimrecovery", AutoNumberClaimRecovery);

            // policy-related auto names/numbers
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_receipt", AutoNumberReceipt);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_policy", AutoNumberPolicyVersion);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_insuredcover", AutoNameInsuredCover);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_insuredcovercommission", AutoNameInsuredCoverCommission);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_policycommission", AutoNamePolicyCommission);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_insuredcovertax", AutoNameInsuredCoverTax);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_insuredcoverreinsurance", AutoNameInsuredCoverReinsurance);

            // reinsurance
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_reinsuranceparticipant", AutoNameReinsuranceParticipant);

            // product related entities
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_risk", AutoNameRiskOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_risk", AutoNameRiskOnUpdate);

            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_cover", AutoNameCoverOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_cover", AutoNameCoverOnUpdate);

            // commissions
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_commissionsalesdetail", AutoNameCommission);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_commissionsalesdetail", AutoNameCommission);

            //Licensed Class
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_licensedclass", AutoNameLicensedClass);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_licensedclass", AutoNameLicensedClass);
        }

        /// <summary>
        /// Automates the number claim on create.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <exception cref="Exception">
        /// Auto Numbering Plugin : Policy Id not found on Claim
        /// or
        /// Auto Numbering Plugin : Could not find policy with Id + policyFolderId.ToString()
        /// or
        /// Auto Numbering Plugin : Could not find broker Id on claim
        /// or
        /// Auto Numbering Plugin : Could not find loss type Id on claim
        /// or
        /// Auto Numbering Plugin : Could not find dateOfLoss on claim
        /// </exception>
        #region AutoNumberClaimOnCreate
        protected void AutoNumberClaimOnCreate(LocalPluginContext context)
        {
            try
            {
                Entity claim = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                Guid policyFolderId = Guid.Empty;

                if (claim.Contains("new_policyid"))
                    policyFolderId = claim.GetAttributeValue<EntityReference>("new_policyid").Id;

                if (policyFolderId == Guid.Empty)
                    throw new Exception("Auto Numbering Plugin : Policy Id not found on Claim");

                Entity policyFolder = context.OrganizationService.Retrieve("new_policyfolder", policyFolderId, new ColumnSet(true));

                if (policyFolder == null)
                    throw new Exception("Auto Numbering Plugin : Could not find policy with Id" + policyFolderId.ToString());

                //retrieve all calims based on the policy
                var retrievedClaim = context.OrganizationService.RetrieveClaim("new_claim", policyFolder.Id);

                //If retrieved claim count is 0 then it is a new claim
                if (retrievedClaim.Count == 0)
                {
                    var policyNumber = policyFolder.GetAttributeValue<string>("new_name");

                    claim["new_name"] = "CL" + "-" + policyNumber + "-" + "001";
                }
                else
                {
                    var oldClaimReference = retrievedClaim.Select(c => c.GetAttributeValue<string>("new_name")).FirstOrDefault();

                    if (oldClaimReference != "")
                    {
                        var oldClaimNumber = oldClaimReference.Substring(oldClaimReference.LastIndexOf('-') + 1);

                        int pad = (oldClaimNumber.ToString()).Trim().Length;

                        int latestClaimreference = retrievedClaim.Count() + 1; //int.Parse(oldClaimNumber) + 1;

                        var latestClaimReferenceNumber = latestClaimreference.ToString("D" + pad.ToString());

                        int index = oldClaimReference.LastIndexOf('-');
                        oldClaimReference = (index > 0 ? oldClaimReference.Substring(0, index) : "");

                        claim["new_name"] = oldClaimReference + "-" + latestClaimReferenceNumber;
                    }
                    else
                    {
                        var retreivedClaimCount = retrievedClaim.Count();

                        var newCount = retreivedClaimCount + 1;

                        var policyNumber = policyFolder.GetAttributeValue<string>("new_name");

                        claim["new_name"] = "CL" + "-" + policyNumber + "-" + newCount.ToString("D3");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion AutoNumberClaimOnCreate

        #region AutoNumberClaimPaymentOnCreate
        /// <summary>
        /// Automatics the number claim payment on create.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <exception cref="Exception"></exception>
        protected void AutoNumberClaimPaymentOnCreate(LocalPluginContext context)
        {
            try
            {
                Entity claimPayment = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                Guid claimId = Guid.Empty;

                Entity claim = new Entity();

                if (claimPayment.Contains("new_claim"))
                {
                    claimId = claimPayment.GetAttributeValue<EntityReference>("new_claim").Id;

                    if (claimId == Guid.Empty)
                        throw new Exception("AutoNumberClaimPaymentOnCreate Plugin : Claim Id cannot be empty");

                    claim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));
                }

                var retrieveClaimPayment = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_payment", claimId);

                string claimPaymentReference = "";

                if (retrieveClaimPayment.Count == 0)
                {
                    if (claim.Contains("new_name"))
                    {
                        var claimReference = claim.GetAttributeValue<string>("new_name");
                        claimReference = claimReference.Replace('L', 'P');
                        claimReference = claimReference.Substring(0, claimReference.LastIndexOf("-") + 1);
                        claimPaymentReference = claimReference + "001";
                        claimPayment["new_name"] = claimPaymentReference;
                    }
                }
                else
                {
                    claimPaymentReference = retrieveClaimPayment.FirstOrDefault().GetAttributeValue<string>("new_name");
                    var claimReferenceNumber = claimPaymentReference.Substring(claimPaymentReference.LastIndexOf('-') + 1);
                    int pad = claimReferenceNumber.Length;
                    var leftSubstring = claimPaymentReference.Substring(0, claimPaymentReference.LastIndexOf('-') + 1);
                    int updatedClaimReferenceNumber = retrieveClaimPayment.Count() + 1; //int.Parse(claimReferenceNumber) + 1;
                    var newClaimReferenceNUmber = updatedClaimReferenceNumber.ToString("D" + pad.ToString());
                    claimPaymentReference = leftSubstring + newClaimReferenceNUmber;
                    claimPayment["new_name"] = claimPaymentReference;
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion AutoNumberClaimtransaction

        /// <summary>
        /// Automatics the number claimtransaction.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <exception cref="Exception">
        /// Claim not set on Claim Transaction
        /// or
        /// </exception>
        #region AutoNumberClaimtransaction
        protected void AutoNumberClaimtransaction(LocalPluginContext context)
        {
            try
            {
                Entity claimTransaction = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                var claimId = Guid.Empty;

                Entity claim = new Entity();

                if (claimTransaction.Contains("new_claim"))
                {
                    claimId = claimTransaction.GetAttributeValue<EntityReference>("new_claim").Id;

                    if (claimId == Guid.Empty)
                        throw new Exception("Claim not set on Claim Transaction");

                    claim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));
                }

                var retrievedClaimTransactions = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_claimtransaction", claimId);

                string claimTransactionName = "";

                if (retrievedClaimTransactions.Count == 0)
                {
                    var claimReference = claim.GetAttributeValue<string>("new_name");
                    claimReference = claimReference.Replace('L', 'T');
                    claimReference = claimReference.Substring(0, claimReference.LastIndexOf("-") + 1);
                    claimTransactionName = claimReference + "001";
                    claimTransaction["new_name"] = claimTransactionName;
                }
                else
                {
                    var latestClaimTransactionName = retrievedClaimTransactions.FirstOrDefault().GetAttributeValue<string>("new_name");
                    latestClaimTransactionName = latestClaimTransactionName.Replace('L', 'T');

                    string latestClaimTransactionNumber = "";

                    if (latestClaimTransactionName != "")
                        latestClaimTransactionNumber = latestClaimTransactionName.Substring(latestClaimTransactionName.LastIndexOf('-') + 1);

                    int pad = latestClaimTransactionNumber.Length;

                    int latestClaimTransactionNo = retrievedClaimTransactions.Count() + 1;    //int.Parse(latestClaimTransactionNumber) + 1;

                    latestClaimTransactionNumber = latestClaimTransactionNo.ToString("D" + pad.ToString());

                    int index = latestClaimTransactionName.LastIndexOf('-');
                    string oldClaimTransactionNumber = (index > 0 ? latestClaimTransactionName.Substring(0, index) : "");
                    claimTransactionName = oldClaimTransactionNumber + "-" + latestClaimTransactionNumber;
                    claimTransaction["new_name"] = claimTransactionName;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }
        }
        #endregion AutoNumberClaimtransaction

        protected void AutoNumberClaimRecovery(LocalPluginContext context)
        {
            try
            {
                Entity claimRecovery = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                var claimId = Guid.Empty;

                Entity claim = new Entity();

                if (claimRecovery.Contains("new_claim"))
                {
                    claimId = claimRecovery.GetAttributeValue<EntityReference>("new_claim").Id;

                    if (claimId == Guid.Empty)
                        throw new Exception("Claim not set on Claim Recovery");

                    claim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));
                }

                var retrievedClaimRecovery = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_claimrecovery", claimId);

                string claimRecoveryName = "";

                if (retrievedClaimRecovery.Count == 0)
                {
                    var claimReference = claim.GetAttributeValue<string>("new_name");
                    claimReference = claimReference.Replace('L', 'R');
                    claimReference = claimReference.Substring(0, claimReference.LastIndexOf("-") + 1);
                    claimRecoveryName = claimReference + "001";
                    claimRecovery["new_name"] = claimRecoveryName;
                }
                else
                {
                    var latestClaimRecoveryName = retrievedClaimRecovery.FirstOrDefault().GetAttributeValue<string>("new_name");
                    latestClaimRecoveryName = latestClaimRecoveryName.Replace('L', 'R');

                    string latestClaimRecoveryNumber = "";

                    if (latestClaimRecoveryName != "")
                        latestClaimRecoveryNumber = latestClaimRecoveryName.Substring(latestClaimRecoveryName.LastIndexOf('-') + 1);

                    int pad = latestClaimRecoveryNumber.Length;

                    int latestClaimRecoveryNo = retrievedClaimRecovery.Count() + 1; //int.Parse(latestClaimRecoveryNumber) + 1;

                    latestClaimRecoveryNumber = latestClaimRecoveryNo.ToString("D" + pad.ToString());

                    int index = latestClaimRecoveryName.LastIndexOf('-');
                    string oldClaimRecoveryNumber = (index > 0 ? latestClaimRecoveryName.Substring(0, index) : "");
                    claimRecoveryName = oldClaimRecoveryNumber + "-" + latestClaimRecoveryNumber;
                    claimRecovery["new_name"] = claimRecoveryName;
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        protected void AutoNumberReceipt(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var policyFolderRef = target.GetAttributeValue<EntityReference>("new_policy");
            if (policyFolderRef == null)
                throw new InvalidPluginExecutionException("Receipt does not contain Policy.");

            var dueDate = target.GetAttributeValue<DateTime?>("new_duedate");
            if (dueDate == null)
                throw new InvalidPluginExecutionException("Receipt does not Due Date.");

            var policyFolder = context.OrganizationService.Retrieve(policyFolderRef);
            var policyNumber = policyFolder.GetAttributeValue<string>("new_name");

            var name = "{0}-R{1:yyyyMMdd}".FormatWith(policyNumber, dueDate);
            target["new_name"] = name;
        }

        protected void AutoNameInsuredCover(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var policyVersionRef = target.GetAttributeValue<EntityReference>("new_policyid");
            if (policyVersionRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover without Policy Version.");

            var coverSectionRef = target.GetAttributeValue<EntityReference>("new_coverid");
            if (coverSectionRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover without Cover Section.");

            var policyVersion = context.OrganizationService.Retrieve(policyVersionRef, "new_name");
            var coverSection = context.OrganizationService.Retrieve(coverSectionRef, "new_name");

            var name = "{0} {1}".FormatWith(policyVersion.GetAttributeValue<string>("new_name"), coverSection.GetAttributeValue<string>("new_name"));
            target["new_name"] = name.LimitLength(100);
        }

        protected void AutoNameInsuredCoverCommission(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var insuredCoverRef = target.GetAttributeValue<EntityReference>("new_insuredcoverid");
            if (insuredCoverRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Commission without Insured Cover.");

            var commissionRef = target.GetAttributeValue<EntityReference>("new_commissionsalesdetailid");
            if (commissionRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Commission without Commission detail.");

            var insuredCover = context.OrganizationService.Retrieve(insuredCoverRef, "new_name");
            var commission = context.OrganizationService.Retrieve(commissionRef, "new_name");

            var name = "{0} {1}".FormatWith(insuredCover.GetAttributeValue<string>("new_name"), commission.GetAttributeValue<string>("new_name"));
            target["new_name"] = name.LimitLength(100);
        }

        protected void AutoNameInsuredCoverReinsurance(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var insuredCoverRef = target.GetAttributeValue<EntityReference>("new_insuredcover");
            if (insuredCoverRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Reinsurance without Insured Cover.");

            var participantRef = target.GetAttributeValue<EntityReference>("new_participant");
            if (participantRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Reinsurance without Participant.");

            var insuredCover = context.OrganizationService.Retrieve(insuredCoverRef, "new_name");
            var participant = context.OrganizationService.Retrieve(participantRef, "new_name");

            var name = "{0} {1}".FormatWith(insuredCover.GetAttributeValue<string>("new_name"), participant.GetAttributeValue<string>("new_name"));
            target["new_name"] = name.LimitLength(100);
        }

        protected void AutoNamePolicyCommission(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var policyVersionRef = target.GetAttributeValue<EntityReference>("new_policyid");
            if (policyVersionRef == null)
                throw new InvalidPluginExecutionException("Cannot create Policy Commission without Policy Version.");

            var policyVersion = context.OrganizationService.Retrieve(policyVersionRef, "new_name");

            var accountRef = target.GetAttributeValue<EntityReference>("new_accountid");
            var contactRef = target.GetAttributeValue<EntityReference>("new_contactid");

            string commissionPartyName;

            if (accountRef != null)
                commissionPartyName = context.OrganizationService.Retrieve(accountRef, "name").GetAttributeValue<string>("name");
            else if (contactRef != null)
                commissionPartyName = context.OrganizationService.Retrieve(contactRef, "fullname").GetAttributeValue<string>("fullname");
            else
                throw new InvalidPluginExecutionException("Cannot create Policy Commission without Contact or Account.");

            var roleRef = target.GetAttributeValue<EntityReference>("new_roleinpolicyid");
            if (roleRef == null)
                throw new InvalidPluginExecutionException("Cannot create Policy Commission without Role in Policy.");

            var role = context.OrganizationService.Retrieve(roleRef, "new_name");

            var name = "{0} - {1} - {2}".FormatWith(policyVersion.GetAttributeValue<string>("new_name"),
                role.GetAttributeValue<string>("new_name"),
                commissionPartyName);

            target["new_name"] = name.LimitLength(100);
        }

        private void AutoNameInsuredCoverTax(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var taxRef = target.GetAttributeValue<EntityReference>("new_taxid");
            if (taxRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Tax without Tax record.");

            var insuredCoverRef = target.GetAttributeValue<EntityReference>("new_insuredcoverid");
            if (insuredCoverRef == null)
                throw new InvalidPluginExecutionException("Cannot create Insured Cover Tax without Insured Cover.");

            var insuredCover = context.OrganizationService.Retrieve(insuredCoverRef, "new_name");
            var tax = context.OrganizationService.Retrieve(taxRef, "new_name");

            var name = "{0} {1}".FormatWith(insuredCover.GetAttributeValue<string>("new_name"), tax.GetAttributeValue<string>("new_name"));
            target["new_name"] = name.LimitLength(100);
        }

        protected void AutoNumberPolicyVersion(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var policyFolderRef = target.GetAttributeValue<EntityReference>("new_policy");
            if (policyFolderRef == null)
                return;

            var policyFolderEntity = context.OrganizationService.Retrieve(policyFolderRef);
            var policyFolder = new Policy(context.OrganizationService, context.TracingService, policyFolderEntity);

            var name = "{0}{1:D2}".FormatWith(policyFolderEntity.GetAttributeValue<string>("new_name"), policyFolder.Versions.Count() + 1);

            target["new_name"] = name;
        }

        protected void AutoNameReinsuranceParticipant(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var percentage = target.GetAttributeValue<decimal>("new_percentage");
            var reference = target.GetAttributeValue<string>("new_reinsurerreference");

            var reinsurerRef = target.GetAttributeValue<EntityReference>("new_reinsurer");
            var reinsurer = context.OrganizationService.Retrieve(reinsurerRef);

            var reinsurarName = reinsurer.GetAttributeValue<string>("name");

            var name = "{0} - {1}% - {2}".FormatWith(reinsurarName, percentage, reference);
            target["new_name"] = name;
        }

        protected void AutoNameRiskOnCreate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var productRef = target.GetAttributeValue<EntityReference>("new_productid");
            if (productRef == null)
                throw new InvalidPluginExecutionException("Risk does not contain product.");

            var riskSubClassRef = target.GetAttributeValue<EntityReference>("new_secondlevelriskclassid");
            if (riskSubClassRef == null)
                throw new InvalidPluginExecutionException("Risk does not contain risk subclass.");

            var product = context.OrganizationService.Retrieve(productRef);
            var riskSubClass = context.OrganizationService.Retrieve(riskSubClassRef);

            var productName = product.GetAttributeValue<string>("new_name");
            var subClassName = riskSubClass.GetAttributeValue<string>("new_name");

            var riskName = "{0} - {1}".FormatWith(productName, subClassName);
            if (riskName.Length > 150)
                riskName = riskName.Substring(0, 150);

            target["new_name"] = riskName;
        }

        protected void AutoNameRiskOnUpdate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var preImage = context.PreImage;

            EntityReference productRef;
            if (target.Contains("new_productid"))
                productRef = target.GetAttributeValue<EntityReference>("new_productid");
            else
                productRef = preImage.GetAttributeValue<EntityReference>("new_productid");

            if (productRef == null)
                throw new InvalidPluginExecutionException("Risk Object does not contain product.");

            EntityReference riskSubClassRef;
            if (target.Contains("new_secondlevelriskclassid"))
                riskSubClassRef = target.GetAttributeValue<EntityReference>("new_secondlevelriskclassid");
            else
                riskSubClassRef = preImage.GetAttributeValue<EntityReference>("new_secondlevelriskclassid");

            if (riskSubClassRef == null)
                throw new InvalidPluginExecutionException("Risk Object does not contain risk subclass.");

            var product = context.OrganizationService.Retrieve(productRef);
            var riskSubClass = context.OrganizationService.Retrieve(riskSubClassRef);

            var productName = product.GetAttributeValue<string>("new_name");
            var subClassName = riskSubClass.GetAttributeValue<string>("new_name");

            var riskName = "{0} - {1}".FormatWith(productName, subClassName);
            if (riskName.Length > 150)
                riskName = riskName.Substring(0, 150);

            target["new_name"] = riskName;
        }

        protected void AutoNameCoverOnCreate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var riskRef = target.GetAttributeValue<EntityReference>("new_riskid");
            if (riskRef == null)
                throw new InvalidPluginExecutionException("Cover section does not contain risk object.");

            var basicCoverRef = target.GetAttributeValue<EntityReference>("new_basiccover");
            if (basicCoverRef == null)
                throw new InvalidPluginExecutionException("Cover section does not contain basic cover section.");

            var risk = context.OrganizationService.Retrieve(riskRef);
            var basicCover = context.OrganizationService.Retrieve(basicCoverRef);

            var riskName = risk.GetAttributeValue<string>("new_name");
            var basicCoverName = basicCover.GetAttributeValue<string>("new_name");

            var coverName = "{0} - {1}".FormatWith(riskName, basicCoverName);
            if (coverName.Length > 150)
                coverName = coverName.Substring(0, 150);

            target["new_name"] = coverName;
        }

        protected void AutoNameCoverOnUpdate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var preImage = context.PreImage;

            EntityReference riskRef;
            if (target.Contains("new_riskid"))
                riskRef = target.GetAttributeValue<EntityReference>("new_riskid");
            else
                riskRef = preImage.GetAttributeValue<EntityReference>("new_riskid");

            if (riskRef == null)
                throw new InvalidPluginExecutionException("Cover section does not contain risk object.");

            EntityReference basicCoverRef;
            if (target.Contains("new_basiccover"))
                basicCoverRef = target.GetAttributeValue<EntityReference>("new_basiccover");
            else
                basicCoverRef = preImage.GetAttributeValue<EntityReference>("new_basiccover");

            if (basicCoverRef == null)
                throw new InvalidPluginExecutionException("Cover section does not contain basic cover section.");

            var risk = context.OrganizationService.Retrieve(riskRef);
            var basicCover = context.OrganizationService.Retrieve(basicCoverRef);

            var riskName = risk.GetAttributeValue<string>("new_name");
            var basicCoverName = basicCover.GetAttributeValue<string>("new_name");

            var coverName = "{0} - {1}".FormatWith(riskName, basicCoverName);
            if (coverName.Length > 150)
                coverName = coverName.Substring(0, 150);

            target["new_name"] = coverName;
        }

        protected void AutoNameCommission(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            Entity preImage = null;

            if (context.PluginExecutionContext.MessageName == "Update")
                preImage = context.PreImage;

            var level = target.GetAttributeWithFallback<OptionSetValue>("new_commissionlevel", preImage);
            if (level == null)
                throw new InvalidPluginExecutionException("Commission does not contain Commission Level.");

            // name for entity which is specified by level (e.g. agreement, product)
            string levelName = null;
            Guid? parentId = null;

            // check if we need to change name - only when commission level base record is changed
            if (level.Value == (int)CommissionLevel.Agreement && !target.Contains("new__agreementid"))
                return;

            if (level.Value == (int)CommissionLevel.Product && !target.Contains("new_productid"))
                return;

            if (level.Value == (int)CommissionLevel.CoverSection && !target.Contains("new_coverid"))
                return;

            if (level.Value == (int)CommissionLevel.Agreement)
            {
                var agreementRef = target.GetAttributeWithFallback<EntityReference>("new__agreementid", preImage);
                if (agreementRef == null)
                    throw new InvalidPluginExecutionException("Commission with level='Agreement' does not contain Agreement.");

                var agreement = context.OrganizationService.Retrieve(agreementRef, "new_name");
                var aggreementName = agreement.GetAttributeValue<string>("new_name");

                levelName = Regex.Match(aggreementName, "^[A-Za-z0-9]+").Value;
                parentId = agreement.Id;

                // clear rest of fields
                target["new_productid"] = null;
                target["new_coverid"] = null;
            }
            else if (level.Value == (int)CommissionLevel.Product)
            {
                var productRef = target.GetAttributeWithFallback<EntityReference>("new_productid", preImage);
                if (productRef == null)
                    throw new InvalidPluginExecutionException("Commission with level='Product' does not contain Product.");

                var product = context.OrganizationService.Retrieve(productRef, "new_code");

                levelName = product.GetAttributeValue<string>("new_code");
                parentId = product.Id;

                // clear rest of fields
                target["new__agreementid"] = null;
                target["new_coverid"] = null;
            }
            else if (level.Value == (int)CommissionLevel.CoverSection)
            {
                var coverRef = target.GetAttributeWithFallback<EntityReference>("new_coverid", preImage);
                if (coverRef == null)
                    throw new InvalidPluginExecutionException("Commission with level='Cover Section' does not contain Cover Section.");

                var cover = context.OrganizationService.Retrieve(coverRef, "new_covercode");

                levelName = cover.GetAttributeValue<string>("new_covercode");
                parentId = cover.Id;

                // clear rest of fields
                target["new_productid"] = null;
                target["new__agreementid"] = null;
            }

            int lastNumber = 0;
            var commissions = RetrieveCommissions(context.OrganizationService, parentId.Value, (CommissionLevel)level.Value);
            var latestCommission = commissions.FirstOrDefault();

            if (latestCommission != null && latestCommission.Id != target.Id)
            {
                var latestCommissionName = latestCommission.GetAttributeValue<string>("new_name");
                var numberMatch = Regex.Match(latestCommissionName, @"COM(?<num>\d\d)$");

                if (numberMatch.Success)
                {
                    var numberPart = numberMatch.Groups["num"].Value;
                    int.TryParse(numberPart, out lastNumber);
                }
            }

            var commissionName = "{0} - COM{1:00}".FormatWith(levelName, lastNumber + 1);
            if (commissionName.Length > 100)
                commissionName = commissionName.Substring(0, 150);

            target["new_name"] = commissionName;
        }

        protected void AutoNameLicensedClass(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            Entity preImage = null;

            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            if (context.PluginExecutionContext.MessageName == "Update")
                preImage = context.PreImage;

            var regulatoryClass = target.GetAttributeWithFallback<EntityReference>("new_regulatoryclass", preImage);
            var territory = target.GetAttributeWithFallback<EntityReference>("new_territory", preImage);
            var licenseType = target.GetAttributeWithFallback<OptionSetValue>("new_licensetype", preImage);
            var retrievedRegulatoryClass = context.OrganizationService.Retrieve(regulatoryClass.LogicalName, regulatoryClass.Id, new ColumnSet(true));
            var regulatoryClassCode = retrievedRegulatoryClass.GetAttributeValue<string>("new_regulatoryclasscode");
            if(regulatoryClassCode == null)
                throw new InvalidPluginExecutionException("Regulatory class code cannot be empty");
            var licenseTypeLabel = context.OrganizationService.GetOptionSetValueLabel("new_licensedclass", "new_licensetype", licenseType.Value);
            var retrievedTerritory = context.OrganizationService.Retrieve(territory.LogicalName, territory.Id, new ColumnSet(true));
            var territoryName = retrievedTerritory.GetAttributeValue<string>("name");

            target["new_name"] = regulatoryClassCode + "-" + territoryName + "-" + licenseTypeLabel;



        }

        private static IEnumerable<Entity> RetrieveCommissions(IOrganizationService svc, Guid parentId, CommissionLevel level)
        {
            var commQuery = new QueryExpression("new_commissionsalesdetail");
            commQuery.AddOrder("new_name", OrderType.Ascending);
            commQuery.ColumnSet.AddColumn("new_name");

            switch (level)
            {
                case CommissionLevel.Agreement:
                    commQuery.Criteria.AddCondition("new__agreementid", ConditionOperator.Equal, parentId);
                    break;
                case CommissionLevel.Product:
                    commQuery.Criteria.AddCondition("new_productid", ConditionOperator.Equal, parentId);
                    break;
                case CommissionLevel.CoverSection:
                    commQuery.Criteria.AddCondition("new_coverid", ConditionOperator.Equal, parentId);
                    break;
                default:
                    break;
            }

            return svc.RetrieveMultiple(commQuery).Entities;
        }

        public static string GetOptionSetValueLabel(IOrganizationService svc, string entityName, string fieldName, int optionSetValue)
        {
            var attReq = new RetrieveAttributeRequest();
            attReq.EntityLogicalName = entityName;
            attReq.LogicalName = fieldName;
            attReq.RetrieveAsIfPublished = true;

            var attResponse = (RetrieveAttributeResponse)svc.Execute(attReq);
            var attMetadata = (EnumAttributeMetadata)attResponse.AttributeMetadata;

            return attMetadata.OptionSet.Options.Where(x => x.Value == optionSetValue).FirstOrDefault().Label.UserLocalizedLabel.Label;

        }
    }
}
