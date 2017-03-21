using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Reinsurance
{
    public class ReinsurancePlugin : BasePlugin
    {
        public ReinsurancePlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            //RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_insuredcover", CreateQuotaShareReinsurance);
            //RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_insuredcover", CreateQuotaShareReinsurance);

            //RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_claim", CreateClaimRecoveries);
        }

        //protected void CreateQuotaShareReinsurance(LocalPluginContext context)
        //{
        //    var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

        //    Entity preImage = null;
        //    if (context.PluginExecutionContext.MessageName == "Update")
        //        preImage = context.PreImage;

        //    // if gross and net premiums are missing, then reinsurance values don't need to be updated
        //    if (!target.Contains("new_grosspremium") && !target.Contains("new_netoftaxpremium"))
        //        return;

        //    var grossPrem = target.GetAttributeValue<Money>("new_grosspremium");
        //    var netPrem = target.GetAttributeValue<Money>("new_netoftaxpremium");

        //    // performance optimization for Updates - skip any re-insurance check and updates if premium values are same as 
        //    // in preImage
        //    if (preImage != null)
        //    {
        //        var preGrossPrem = preImage.GetAttributeValue<Money>("new_grosspremium");
        //        var preNetPrem = preImage.GetAttributeValue<Money>("new_netoftaxpremium");

        //        // same premium as in pre-image, nothing to do
        //        if (preGrossPrem != null && grossPrem != null && preGrossPrem.Value == grossPrem.Value)
        //            return;

        //        if (preNetPrem != null && netPrem != null && preNetPrem.Value == netPrem.Value)
        //            return;
        //    }

        //    var insuredCover = new InsuredCover(context.OrganizationService, context.TracingService, target);
        //    insuredCover.Reload();

        //    var reinsuranceContract = RetrieveReinsuranceContract(context, insuredCover, ReinsuranceSectionMethod.QuotaShare);

        //    ReinsuranceSection quotaShareSection = null;

        //    if (reinsuranceContract != null)
        //    {
        //        quotaShareSection = reinsuranceContract.ReinsuranceSections.First();
        //        if (quotaShareSection.CededPercentage == null)
        //            throw new InvalidPluginExecutionException("Incorrect Quota Share Reinsurance section. Ceded percentage is empty.");

        //        var reinsurancePremiums = CalculateQuotaShareForInsuredCover(context, quotaShareSection, insuredCover);
        //        UpdateReinsuranceTotals(context, insuredCover, reinsurancePremiums);
        //    }
        //    else
        //    {
        //        UpdateReinsuranceTotals(context, insuredCover, 0);
        //    }
        //}

        //private void CreateClaimRecoveries(LocalPluginContext context)
        //{
        //    var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

        //    if (!target.Contains("new_claimedamount"))
        //        return;

        //    var postImage = context.PostImage;
        //    var insuredCover = new InsuredCover(context.OrganizationService, context.TracingService, postImage);

        //    var claimedAmt = target.GetAttributeValue<Money>("new_claimedamount");

        //    // 1. try to get quota share treaties
        //    var quotaShareContract = RetrieveReinsuranceContract(context, insuredCover, ReinsuranceSectionMethod.QuotaShare);

        //    if (quotaShareContract != null)
        //    {
        //        // process quota share reinsurance
        //        // - get reinsurance section
        //        // - get participants
        //        // - claimed amount divide between them
        //        return;
        //    }


        //    // 2. if no quota share, retrieve XoL
        //    //  - process stack



        //    // 3. (?) check catastrophe XoL?

        //    // creating claim recoveries
        //    // - account = participant account
        //    // - recovery date = ??
        //    // - 

        //    var recovery = new Entity("new_claimrecovery");
        //    recovery["new_recoveryfromcompany"] = null; // reinsurance participant

        //}

        ///// <summary>
        ///// Updates totals of reinsurance premium values, 
        ///// </summary>
        ///// <param name="context"></param>
        ///// <param name="insuredCover"></param>
        ///// <param name="premium"></param>
        //private static void UpdateReinsuranceTotals(LocalPluginContext context, InsuredCover insuredCover, decimal cededPremium)
        //{
        //    // update insured cover with new premium values
        //    var updatedInsuredCover = new Entity(insuredCover.LogicalName) { Id = insuredCover.Entity.Id };

        //    var insCoverGrossPremium = insuredCover.GrossPremium == null ? 0 : insuredCover.GrossPremium.Value;
        //    var insCoverNetPremium = insuredCover.NetOfTaxAndCommissionsPremium == null ? 0 : insuredCover.NetOfTaxAndCommissionsPremium.Value;

        //    updatedInsuredCover["new_cededpremium"] = new Money(cededPremium);
        //    updatedInsuredCover["new_grossretainedpremium"] = new Money(insCoverGrossPremium - cededPremium);
        //    updatedInsuredCover["new_netretainedpremium"] = new Money(insCoverNetPremium - cededPremium);

        //    context.OrganizationService.Update(updatedInsuredCover);

        //    var insuredRisk = insuredCover.InsuredRisk;
        //    var insuredCoversOfRisk = insuredRisk.InsuredCovers;

        //    // calculate totals for insured risk
        //    var riskTotalCeded = insuredCoversOfRisk.Sum(c => c.CededPremium.ToDecimal());
        //    var riskTotalRetainedGross = insuredCoversOfRisk.Sum(c => c.GrossRetainedPremium.ToDecimal());
        //    var riskTotalRetainedNet = insuredCoversOfRisk.Sum(c => c.NetRetainedPremium.ToDecimal());

        //    // update insured risk
        //    var riskToUpdate = new Entity(insuredRisk.LogicalName) { Id = insuredRisk.Id };
        //    riskToUpdate["new_cededpremium"] = new Money(riskTotalCeded.Value);
        //    riskToUpdate["new_grossretainedpremium"] = new Money(riskTotalRetainedGross.Value);
        //    riskToUpdate["new_netretainedpremium"] = new Money(riskTotalRetainedNet.Value);
        //    context.OrganizationService.Update(riskToUpdate);

        //    var policyVersion = insuredRisk.PolicyVersion;
        //    var insuredCoversOfPolicy = policyVersion.InsuredCovers;

        //    // calculate totals for policy version
        //    var policyTotalCeded = insuredCoversOfPolicy.Sum(c => c.CededPremium.ToDecimal());
        //    var policyTotalRetainedGross = insuredCoversOfPolicy.Sum(c => c.GrossRetainedPremium.ToDecimal());
        //    var policyTotalRetainedNet = insuredCoversOfPolicy.Sum(c => c.NetRetainedPremium.ToDecimal());

        //    // update policy version
        //    var policyVersionToUpdate = new Entity(policyVersion.LogicalName) { Id = policyVersion.Id };
        //    policyVersionToUpdate["new_cededpremium"] = new Money(policyTotalCeded.Value);
        //    policyVersionToUpdate["new_grossretainedpremium"] = new Money(policyTotalRetainedGross.Value);
        //    policyVersionToUpdate["new_netretainedpremium"] = new Money(policyTotalRetainedNet.Value);
        //    context.OrganizationService.Update(policyVersionToUpdate);
        //}
    }
}
