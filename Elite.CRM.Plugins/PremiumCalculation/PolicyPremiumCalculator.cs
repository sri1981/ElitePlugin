using Elite.CRM.Plugins.Entities;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.PremiumCalculation
{
    /// <summary>
    /// Calculates premium for policy version. Before using premium calculator, make sure that all insured risks and insured covers
    /// are properly set up.
    /// </summary>
    class PolicyPremiumCalculator
    {
        private static int DecimalPrecision = 2;

        private IOrganizationService _svc;
        private Product _product;
        private PolicyVersion _policyVersion;

        private PremiumDefinition _policyTotals = new PremiumDefinition();
        private IEnumerable<InsuredCoverPremiumDefinition> _insuredCoversPremiums;

        private CoverPremiumTypeAllowed _premiumCalculation;

        public PolicyPremiumCalculator(IOrganizationService svc, Product product, PolicyVersion policyVersion)
        {
            _svc = svc;
            _product = product;
            _policyVersion = policyVersion;

            // we check first cover of product, because the field (agreed/calculated) is on cover, not product
            var firstCoverSection = product.Covers.First();
            if (firstCoverSection.PremiumType == null)
                throw new Exception("Invalid product configuration, 'Premium type allowed' field is empty.");

            _premiumCalculation = firstCoverSection.PremiumType.Value;

            // initialize collection of calculated premiums to empty values
            _insuredCoversPremiums = _policyVersion.InsuredCovers.Select(ic => new InsuredCoverPremiumDefinition()
            {
                InsuredCover = ic,
                Cover = product.Covers.FirstOrDefault(cs => cs.Id == ic.CoverRef.Id)
            }).ToList();
        }

        /// <summary>
        /// Calculates base premium of policy and insured covers.
        /// </summary>
        public void CalculateBasePremium()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            if (_premiumCalculation == CoverPremiumTypeAllowed.Calculated)
            {
                foreach (var premiumDef in _insuredCoversPremiums)
                {
                    // set base premium to premium value from cover
                    premiumDef.BasePremium = premiumDef.Cover.PremiumAmount.Value;
                }

                // set policy base premium to sum of totals
                _policyTotals.BasePremium = _insuredCoversPremiums.Sum(p => p.BasePremium);
            }
            else if (_premiumCalculation == CoverPremiumTypeAllowed.Agreed)
            {
                if (_policyVersion.GrossPremium == null)
                    throw new Exception("Gross premium of policy version is required for 'Agreed' premium calculation.");

                _policyTotals.BasePremium = _policyVersion.GrossPremium.Value;

                // split among covers 
                var splits = _insuredCoversPremiums.SplitAmount(_policyTotals.BasePremium, p => p.Cover.CoverWeight, DecimalPrecision);
                foreach (var s in splits)
                {
                    var coverPremium = s.Item1;

                    // update premium with split value
                    coverPremium.BasePremium = s.Item2;
                }
            }
        }

        public void CalculateRiskFactors()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            if (_premiumCalculation == CoverPremiumTypeAllowed.Calculated)
            {
                throw new NotImplementedException("Risk factors not implemented!");
            }
            else if (_premiumCalculation == CoverPremiumTypeAllowed.Agreed)
            {
                // risk factors are not applicable for agreed premium type
                _policyTotals.RiskFactorPremium = 0;

                foreach (var ic in _insuredCoversPremiums)
                {
                    ic.RiskFactorPremium = 0;
                }
            }
        }

        public void CalculateGrossPremium()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            // calculate gross premium for policy from totals
            _policyTotals.GrossPremium = _policyTotals.BasePremium - _policyTotals.RiskFactorPremium;

            // calculate gross premium for individual insured covers
            foreach (var premium in _insuredCoversPremiums)
            {
                premium.GrossPremium = premium.BasePremium - premium.RiskFactorPremium;
            }

            var percentSplits = _insuredCoversPremiums.SplitAmount(100, ic => ic.GrossPremium, DecimalPrecision);
            foreach (var split in percentSplits)
            {
                // set percentage to split value
                split.Item1.PremiumPercentage = split.Item2;
            }
        }

        public void CalculateTax()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            var applicableTaxes = _product.ApplicableTaxes;

            // these are taxes without any specific basic cover (peril) or regulatory class
            var countryLevelTaxes = applicableTaxes
                    .Where(t => t.BasicCoverRef == null && t.RegulatoryClassRef == null) // only country-level taxes apply
                    .OrderBy(t => t.ValidUntil, Tax.DateComparer) // ordered by 'valid until'
                    .SkipWhile(t => t.ValidUntil < _policyVersion.TransactionEffectiveDate); // skip all dates before 

            if (_premiumCalculation == CoverPremiumTypeAllowed.Calculated)
            {
                foreach (var premiumDef in _insuredCoversPremiums)
                {
                    // taxes for basic cover (Peril)
                    var taxesForBasicCover = applicableTaxes
                        .ForBasicCover(premiumDef.Cover.RegulatoryClassRef)
                        .SkipWhile(t => t.ValidUntil < _policyVersion.TransactionEffectiveDate);

                    // taxes for regulatory class
                    var taxesForRegulatorClass = applicableTaxes
                        .ForRegulatoryClass(premiumDef.Cover.RegulatoryClassRef)
                        .SkipWhile(t => t.ValidUntil < _policyVersion.TransactionEffectiveDate);

                    // make union of all applicable taxes for current cover
                    var allTaxesForCover = countryLevelTaxes
                        .Union(taxesForBasicCover)
                        .Union(taxesForRegulatorClass);

                    if (!allTaxesForCover.Any())
                        throw new Exception("No taxes defined for cover.");

                    // calculate total percentage of tax to be applied to current cover
                    var totalTaxPercentage = allTaxesForCover.Sum(t => t.TaxPercentage);

                    // calculate total tax for cover
                    premiumDef.GrossExcludingTax = Math.Round(premiumDef.GrossExcludingTax / (100 + totalTaxPercentage.Value) * 100, DecimalPrecision);
                    premiumDef.Tax = premiumDef.GrossPremium - premiumDef.GrossExcludingTax;

                    // split total cover tax among tax records by percentage
                    var taxSplits = allTaxesForCover.SplitAmount(premiumDef.Tax, t => t.TaxPercentage, DecimalPrecision);

                    // set tax definition to collection of taxes 
                    premiumDef.TaxDefinitions = taxSplits.Select(ts =>
                    {
                        var tax = ts.Item1;
                        var taxAmount = ts.Item2;

                        return new TaxDefinition()
                        {
                            Amount = taxAmount,
                            TaxId = tax.Id,
                            Percentage = tax.TaxPercentage.Value
                        };
                    }).ToList();
                }
            }
            else if (_premiumCalculation == CoverPremiumTypeAllowed.Agreed)
            {
                // pick up the first tax, which is still valid
                var tax = countryLevelTaxes.FirstOrDefault();

                if (tax == null)
                    throw new Exception("No taxes defined for cover.");

                // calculate tax values for policy version
                _policyTotals.GrossExcludingTax = Math.Round(_policyTotals.GrossPremium / (100 + tax.TaxPercentage.Value) * 100, DecimalPrecision);
                _policyTotals.Tax = _policyTotals.GrossPremium - _policyTotals.GrossExcludingTax;

                // split tax amounts among insured covers
                var taxSplits = _insuredCoversPremiums.SplitAmount(_policyTotals.Tax, ic => ic.GrossPremium, DecimalPrecision);
                foreach (var taxSplit in taxSplits)
                {
                    var icPremiums = taxSplit.Item1;
                    var taxAmount = taxSplit.Item2;

                    // set insured cover tax amounts
                    icPremiums.Tax = taxAmount;
                    icPremiums.GrossExcludingTax = icPremiums.GrossPremium - icPremiums.Tax;

                    // create tax definition to create Insured Cover Tax record later
                    icPremiums.TaxDefinitions.Add(new TaxDefinition()
                    {
                        Amount = taxAmount,
                        TaxId = tax.Id,
                        Percentage = tax.TaxPercentage.Value
                    });
                }
            }
        }

        public void CalculateCommission()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            // all commissions for either product or agreement level
            var productCommissions = _product.Commissions
                .Where(c => c.StartDate < _policyVersion.TransactionEffectiveDate &&
                            c.EndDate > _policyVersion.TransactionEffectiveDate); // filter by transaction date

            if (_premiumCalculation == CoverPremiumTypeAllowed.Calculated)
            {
                foreach (var premiumDefinition in _insuredCoversPremiums)
                {
                    // TODO include commission-per-cover
                    var percentageCommissions = productCommissions.Where(c => c.Method == CommissionMethod.Percentage);

                    var totalPercentage = percentageCommissions.Sum(c => c.CommissionPercentage);

                    // amount calculated by total percentage of all %commissions
                    var commissionAmountFromPercentage = Math.Round(premiumDefinition.GrossPremium * totalPercentage / 100, DecimalPrecision);
                    var percentageSplits = percentageCommissions.SplitAmount(commissionAmountFromPercentage, c => c.CommissionPercentage, DecimalPrecision);

                    foreach (var split in percentageSplits)
                    {
                        var commission = split.Item1;
                        var amount = split.Item2;

                        premiumDefinition.CommissionDefinitions.Add(commission.CreateDefinition(amount));
                    }

                    var amountCommissions = productCommissions.Where(c => c.Method == CommissionMethod.Amount);

                    foreach (var commisson in amountCommissions)
                    {
                        premiumDefinition.CommissionDefinitions.Add(commisson.CreateDefinition(commisson.CommissionAmount.Value));
                    }

                    premiumDefinition.Commission = premiumDefinition.CommissionDefinitions.Sum(c => c.Amount);
                    premiumDefinition.NetOfCommission = premiumDefinition.GrossPremium - premiumDefinition.Commission;
                }
            }
            else if (_premiumCalculation == CoverPremiumTypeAllowed.Agreed)
            {
                if (productCommissions.Any(c => c.Method == CommissionMethod.ProvidedByBroker)) // single commission set by broker on policy level
                {
                    // commission definition, which is to be linked to Insured Cover Commissions
                    var commissionRecord = productCommissions.First(c => c.Method == CommissionMethod.ProvidedByBroker);

                    var policyLevelCommission = _policyVersion.Commission;
                    if (policyLevelCommission == null)
                        throw new Exception("Policy does not contain commission amount. Cannot calculate commission.");

                    var commissionSplits = _insuredCoversPremiums
                        .SplitAmount(policyLevelCommission.Value, ic => ic.GrossPremium, DecimalPrecision);

                    foreach (var split in commissionSplits)
                    {
                        var premium = split.Item1;
                        var commissionAmnt = split.Item2;

                        // set single commission amount for insured cover
                        premium.Commission = commissionAmnt;
                        premium.CommissionDefinitions.Add(commissionRecord.CreateDefinition(commissionAmnt));
                    }
                }
                else // multiple commissions, either percentage or amounts
                {
                    // 1. === percentage commissions ===
                    var percentageCommissions = productCommissions.Where(c => c.Method == CommissionMethod.Percentage);
                    var totalPercentage = percentageCommissions.Sum(c => c.CommissionPercentage);

                    // amount calculated by total percentage of all %commissions
                    var commissionAmountFromPercentage = Math.Round(_policyTotals.GrossPremium * totalPercentage / 100, DecimalPrecision);

                    // split total amount of commissions among all %commissions
                    var splitPerCommisson = percentageCommissions.SplitAmount(commissionAmountFromPercentage, c => c.CommissionPercentage, DecimalPrecision);

                    foreach (var split in splitPerCommisson)
                    {
                        // commission definition from product
                        var commission = split.Item1;
                        var commissionAmount = split.Item2;

                        // now split it among insured covers
                        var insuredCoverSplits = _insuredCoversPremiums.SplitAmount(commissionAmount, ic => ic.GrossPremium, DecimalPrecision);
                        foreach (var icSplit in insuredCoverSplits)
                        {
                            var icPremium = icSplit.Item1;
                            var icCommission = icSplit.Item2;

                            icPremium.CommissionDefinitions.Add(commission.CreateDefinition(icCommission));
                        }
                    }

                    // 2. === amount commissions ===
                    var amountCommissions = productCommissions.Where(c => c.Method == CommissionMethod.Amount);

                    foreach (var comm in amountCommissions)
                    {
                        IEnumerable<Tuple<InsuredCoverPremiumDefinition, decimal>> insuredCoverSplits = null;
                        var policyVersionTransactionType = _policyVersion.TransactionType;

                        // split commission among all insured covers
                        if(policyVersionTransactionType.HasValue && policyVersionTransactionType.Value == PolicyVersionTransactionType.Cancellation)
                        {
                            insuredCoverSplits = _insuredCoversPremiums.SplitAmount(comm.CommissionAmount.Value * -1, ic => ic.GrossPremium, DecimalPrecision);
                        }
                        else
                        {
                            insuredCoverSplits = _insuredCoversPremiums.SplitAmount(comm.CommissionAmount.Value, ic => ic.GrossPremium, DecimalPrecision);
                        }

                        foreach (var split in insuredCoverSplits)
                        {
                            var icPremium = split.Item1;
                            var icCommissionAmount = split.Item2;

                            // add commission definition to insured cover
                            icPremium.CommissionDefinitions.Add(comm.CreateDefinition(icCommissionAmount));
                        }
                    }
                }
            }

            // after commission definitions are created, sum them up to get total commission amounts for insured covers
            foreach (var premium in _insuredCoversPremiums)
            {
                premium.Commission = premium.CommissionDefinitions.Sum(c => c.Amount);
                premium.NetOfCommission = premium.GrossPremium - premium.Commission;
                premium.NetOfTaxAndCommission = premium.NetOfCommission - premium.Tax;
            }

            // and also get the total amount of commissions for policy version
            _policyTotals.Commission = _insuredCoversPremiums.Sum(ic => ic.Commission);
            _policyTotals.NetOfCommission = _policyTotals.GrossPremium - _policyTotals.Commission;
            _policyTotals.NetOfTaxAndCommission = _policyTotals.NetOfCommission - _policyTotals.Tax;
        }

        /// <summary>
        /// Calculates quota share reinsurance for insured covers.
        /// </summary>
        public void CalculateReinsurance()
        {
            foreach (var premium in _insuredCoversPremiums)
            {
                // get reinsurance contract
                var reinsContract = premium.InsuredCover.RetrieveReinsuranceContract(ReinsuranceSectionMethod.QuotaShare);

                // if there's no reinsurance contract, no reinsurance to calculate
                if (reinsContract == null)
                {
                    premium.CededPremium = 0;
                    premium.GrossRetainedPremium = premium.GrossPremium;
                    premium.NetRetainedPremium = premium.NetOfTaxAndCommission;

                    continue;
                }
                
                // there should be a single section for quota share
                ReinsuranceSection quotaShareSection = reinsContract.ReinsuranceSections.First();
                if (quotaShareSection.CededPercentage == null)
                    throw new InvalidPluginExecutionException("Incorrect Quota Share Reinsurance section. Ceded percentage is empty.");

                // get correct (gross/net) premium to use for reinsurance calculation
                decimal insuredCoverPremium = 0;

                if (quotaShareSection.ReinsuranceCalculation == ReinsuranceSectionCalculation.GrossPremium)
                    insuredCoverPremium = premium.InsuredCover.GrossPremium.Value;
                else if (quotaShareSection.ReinsuranceCalculation == ReinsuranceSectionCalculation.NetPremium)
                    insuredCoverPremium = premium.InsuredCover.NetOfTaxAndCommissionsPremium.Value;
                
                var cededPercentage = quotaShareSection.CededPercentage;
                premium.CededPremium = Math.Round(insuredCoverPremium * (cededPercentage.Value / 100), DecimalPrecision);
                premium.GrossRetainedPremium = premium.GrossPremium - premium.CededPremium;
                premium.NetRetainedPremium = premium.NetOfTaxAndCommission - premium.CededPremium;

                var splitPremiums = quotaShareSection.Participants.SplitAmount(premium.CededPremium, p => p.GetAttributeValue<decimal?>("new_percentage"), DecimalPrecision);

                // create insured cover reinsurances
                foreach (var split in splitPremiums)
                {
                    var participant = split.Item1;
                    var amounts = split.Item2;

                    premium.ReinsuranceDefinitions.Add(new ReinsuranceDefinition() 
                    {
                        CededAmount = amounts,
                        ParticipantRef = participant.ToEntityReference(),
                        ReinsuranceSectionId = quotaShareSection.Id
                    });
                }
            }

            // get totals for policy version, sum all insured covers
            _policyTotals.CededPremium = _insuredCoversPremiums.Sum(ic => ic.CededPremium);
            _policyTotals.GrossRetainedPremium = _insuredCoversPremiums.Sum(ic => ic.GrossRetainedPremium);
            _policyTotals.NetRetainedPremium = _insuredCoversPremiums.Sum(ic => ic.NetRetainedPremium);
        }

        /// <summary>
        /// Updates policy version, insured risks and insured covers. Creates/updates all insured risk related records (taxes, commissions, etc.)
        /// with calculated values. Updates totals of policy and insured risks.
        /// </summary>
        public void UpdatePremiums()
        {
            if (!_insuredCoversPremiums.Any())
                return;

            var insuredCoverTaxes = _policyVersion.RetrieveInsuredCoverTaxes();
            var insuredCoverCommissions = _policyVersion.RetrieveInsuredCoverCommissions();
            var insuredCoverReinsurances = _policyVersion.RetrieveInsuredCoverReinsurances();

            foreach (var premium in _insuredCoversPremiums)
            {
                // create/update insured cover taxes with calculated tax values
                foreach (var taxDef in premium.TaxDefinitions)
                {
                    // get existing insured cover tax from retrieved records
                    var existingTax = insuredCoverTaxes.FirstOrDefault(icTax => 
                        premium.InsuredCover.Id == icTax.GetAttributeValue<EntityReference>("new_insuredcoverid").Id &&
                        taxDef.TaxId == icTax.GetAttributeValue<EntityReference>("new_taxid").Id);

                    if (existingTax == null)
                    {
                        existingTax = new Entity("new_insuredcovertax");
                        existingTax["new_taxid"] = new EntityReference("new_regionalsettings", taxDef.TaxId);
                        existingTax["new_insuredcoverid"] = premium.InsuredCover.EntityReference;
                    }

                    existingTax["new_taxamount"] = new Money(taxDef.Amount);
                    existingTax["new_taxpercentage"] = taxDef.Percentage;

                    _svc.CreateOrUpdateEntity(ref existingTax);
                }

                // create/update insured cover commissions with commission values
                foreach (var commDef in premium.CommissionDefinitions)
                {
                    // get existing insured cover commission from retrieved records
                    var existingCommission = insuredCoverCommissions.FirstOrDefault(icCommission =>
                        premium.InsuredCover.Id == icCommission.GetAttributeValue<EntityReference>("new_insuredcoverid").Id &&
                        commDef.CommissionId == icCommission.GetAttributeValue<EntityReference>("new_commissionsalesdetailid").Id);

                    if (existingCommission == null)
                    {
                        existingCommission = new Entity("new_insuredcovercommission");
                        existingCommission["new_insuredcoverid"] = premium.InsuredCover.EntityReference;
                        existingCommission["new_commissionsalesdetailid"] = new EntityReference("new_commissionsalesdetail", commDef.CommissionId);
                    }

                    existingCommission["new_commission"] = new Money(commDef.Amount);

                    _svc.CreateOrUpdateEntity(ref existingCommission);
                }

                // create/update insured cover reinsurances
                foreach (var reinsDef in premium.ReinsuranceDefinitions)
                {
                    //var existingReinsurance = premium.InsuredCover.RetrieveInsuredCoverReinsurance(reinsDef.ReinsuranceSectionId, reinsDef.ParticipantRef.Id);
                    // get existing insured cover reinsurance from retrieved records
                    var existingReinsurance = insuredCoverReinsurances.FirstOrDefault(icReins =>
                        premium.InsuredCover.Id == icReins.GetAttributeValue<EntityReference>("new_insuredcover").Id &&
                        reinsDef.ParticipantRef.Id == icReins.GetAttributeValue<EntityReference>("new_participant").Id &&
                        reinsDef.ReinsuranceSectionId == icReins.GetAttributeValue<EntityReference>("new_reinsurancecontract").Id);

                    if (existingReinsurance == null)
                    {
                        existingReinsurance = new Entity("new_insuredcoverreinsurance");
                        existingReinsurance["new_insuredcover"] = premium.InsuredCover.EntityReference;
                        existingReinsurance["new_participant"] = reinsDef.ParticipantRef;
                        existingReinsurance["new_reinsurancecontract"] = new EntityReference("new_reinscontract", reinsDef.ReinsuranceSectionId);
                    }

                    existingReinsurance["new_cededamount"] = new Money(reinsDef.CededAmount);

                    _svc.CreateOrUpdateEntity(ref existingReinsurance);
                }

                // update insured cover
                var updatedInsuredCover = new Entity(premium.InsuredCover.LogicalName) { Id = premium.InsuredCover.Id };
                updatedInsuredCover["new_basepremium"] = new Money(premium.GrossPremium);
                updatedInsuredCover["new_riskfactorpremium"] = new Money(premium.RiskFactorPremium);
                updatedInsuredCover["new_grosspremium"] = new Money(premium.GrossPremium);
                updatedInsuredCover["new_grossexcludingtax"] = new Money(premium.GrossExcludingTax);
                updatedInsuredCover["new_tax"] = new Money(premium.Tax);
                updatedInsuredCover["new_commission"] = new Money(premium.Commission);
                updatedInsuredCover["new_netpremium"] = new Money(premium.NetOfCommission);
                updatedInsuredCover["new_netoftaxpremium"] = new Money(premium.NetOfTaxAndCommission);

                updatedInsuredCover["new_cededpremium"] = new Money(premium.CededPremium);
                updatedInsuredCover["new_grossretainedpremium"] = new Money(premium.GrossRetainedPremium);
                updatedInsuredCover["new_netretainedpremium"] = new Money(premium.NetRetainedPremium);
                
                updatedInsuredCover["new_premiumonpolicy"] = premium.PremiumPercentage;

                _svc.Update(updatedInsuredCover);
            }

            // update totals on insured risks
            foreach (var insRisk in _policyVersion.InsuredRisks)
            {
                var premiums = _insuredCoversPremiums.Where(prem => insRisk.Id == prem.InsuredCover.InsuredRiskRef.Id);

                var updatedInsuredRisk = new Entity(insRisk.LogicalName) { Id = insRisk.Id };
                updatedInsuredRisk["new_basepremium"] = new Money(premiums.Sum(p => p.BasePremium));
                updatedInsuredRisk["new_riskfactorpremium"] = new Money(premiums.Sum(p => p.RiskFactorPremium));
                updatedInsuredRisk["new_grosspremium"] = new Money(premiums.Sum(p => p.GrossPremium));
                updatedInsuredRisk["new_grossexcludingtax"] = new Money(premiums.Sum(p => p.GrossExcludingTax));
                updatedInsuredRisk["new_tax"] = new Money(premiums.Sum(p => p.Tax));
                updatedInsuredRisk["new_commission"] = new Money(premiums.Sum(p => p.Commission));
                updatedInsuredRisk["new_netpremium"] = new Money(premiums.Sum(p => p.NetOfCommission));
                updatedInsuredRisk["new_netoftaxpremium"] = new Money(premiums.Sum(p => p.NetOfTaxAndCommission));

                updatedInsuredRisk["new_cededpremium"] = new Money(premiums.Sum(p => p.CededPremium));
                updatedInsuredRisk["new_grossretainedpremium"] = new Money(premiums.Sum(p => p.GrossRetainedPremium));
                updatedInsuredRisk["new_netretainedpremium"] = new Money(premiums.Sum(p => p.NetRetainedPremium));
                
                updatedInsuredRisk["new_premiumonpolicy"] = premiums.Sum(p => p.PremiumPercentage);

                _svc.Update(updatedInsuredRisk);
            }

            // update policy version
            var updatedPolicyVersion = new Entity(_policyVersion.LogicalName) { Id = _policyVersion.Id };
            updatedPolicyVersion["new_basepremium"] = new Money(_policyTotals.BasePremium);
            updatedPolicyVersion["new_riskfactorpremium"] = new Money(_policyTotals.RiskFactorPremium);
            updatedPolicyVersion["new_grosspremium"] = new Money(_policyTotals.GrossPremium);
            updatedPolicyVersion["new_grossexcludingtax"] = new Money(_policyTotals.GrossExcludingTax);
            updatedPolicyVersion["new_tax"] = new Money(_policyTotals.Tax);
            updatedPolicyVersion["new_commission"] = new Money(_policyTotals.Commission);
            updatedPolicyVersion["new_netpremium"] = new Money(_policyTotals.NetOfCommission);
            updatedPolicyVersion["new_netoftaxpremium"] = new Money(_policyTotals.NetOfTaxAndCommission);

            updatedPolicyVersion["new_cededpremium"] = new Money(_policyTotals.CededPremium);
            updatedPolicyVersion["new_grossretainedpremium"] = new Money(_policyTotals.GrossRetainedPremium);
            updatedPolicyVersion["new_netretainedpremium"] = new Money(_policyTotals.NetRetainedPremium);

            _svc.Update(updatedPolicyVersion);
            
            // update policy commission
            var commissionsGroups = _insuredCoversPremiums
                .SelectMany(ic => ic.CommissionDefinitions)
                .GroupBy(c => new { c.ParticipantRef, c.RoleTypeId});

            foreach (var group in commissionsGroups)
            {
                // combination of role type ID and company/contact ID
                var groupingKey = group.Key;

                var existingCommission = _policyVersion.RetrievePolicyCommission(groupingKey.ParticipantRef, groupingKey.RoleTypeId);

                if (existingCommission == null)
                {
                    existingCommission = new Entity("new_policycommission");
                    
                    if (groupingKey.ParticipantRef.LogicalName == "account")
                       existingCommission["new_accountid"] = groupingKey.ParticipantRef;
                    else if (groupingKey.ParticipantRef.LogicalName == "contact")
                       existingCommission["new_contactid"] = groupingKey.ParticipantRef;

                    existingCommission["new_roleinpolicyid"] = new EntityReference("new_roletype", groupingKey.RoleTypeId);
                    existingCommission["new_policyid"] = _policyVersion.EntityReference;
                }

                existingCommission["new_commission"] = new Money(group.Sum(c => c.Amount));

                _svc.CreateOrUpdateEntity(ref existingCommission);
            }
        }
    }
}
