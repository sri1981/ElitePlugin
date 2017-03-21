using Elite.CRM.Plugins.Entities;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.PremiumCalculation
{
    class PremiumDefinition
    {
        // Some link to actual entities
        public Cover Cover { get; set; }
        public InsuredCover InsuredCover { get; set; }

        // premium data
        public decimal BasePremium { get; set; }
        public decimal RiskFactorPremium { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal GrossExcludingTax { get; set; }
        public decimal Tax { get; set; }
        public decimal Commission { get; set; }
        public decimal NetOfCommission { get; set; }
        public decimal NetOfTaxAndCommission { get; set; }

        // reinsurance data
        public decimal CededPremium { get; set; }
        public decimal GrossRetainedPremium { get; set; }
        public decimal NetRetainedPremium { get; set; }
    }

    class InsuredCoverPremiumDefinition : PremiumDefinition
    {
        public InsuredCoverPremiumDefinition() 
        {
            // initialize to empty lists
            RiskFactorDefinitions = new List<RiskFactorDefinition>();
            TaxDefinitions = new List<TaxDefinition>();
            CommissionDefinitions = new List<CommissionDefinition>();
            ReinsuranceDefinitions = new List<ReinsuranceDefinition>();
        }

        public IList<RiskFactorDefinition> RiskFactorDefinitions { get; set; }
        public IList<TaxDefinition> TaxDefinitions { get; set; }
        public IList<CommissionDefinition> CommissionDefinitions { get; set; }
        public IList<ReinsuranceDefinition> ReinsuranceDefinitions { get; set; }

        // percentage of policy premium
        public decimal PremiumPercentage { get; set; }
    }

    class TaxDefinition
    {
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public Guid TaxId { get; set; }
    }

    class CommissionDefinition
    {
        public Guid CommissionId { get; set; }
        public Guid RoleTypeId { get; set; }
        public EntityReference ParticipantRef { get; set; }
        public decimal Amount { get; set; }
    }

    class ReinsuranceDefinition
    {
        public decimal CededAmount { get; set; }
        public Guid ReinsuranceSectionId { get; set; }
        public EntityReference ParticipantRef { get; set; }
    }

    class RiskFactorDefinition
    {

    }

}
