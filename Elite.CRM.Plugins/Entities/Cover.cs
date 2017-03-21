using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    enum CoverPremiumTypeAllowed
    {
        Agreed = 100000000,
        Calculated = 100000001,
    }

    /// <summary>
    /// Now, it's called Peril Section.
    /// </summary>
    class Cover : EntityWrapper
    {
        public override string LogicalName { get { return "new_cover"; } }

        public Cover(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public CoverPremiumTypeAllowed? PremiumType
        {
            get
            {
                var premType = this.Entity.GetAttributeValue<OptionSetValue>("new_premiumtypeallowed");
                if (premType == null)
                    return null;

                return premType.ToEnum<CoverPremiumTypeAllowed>();
            }
        }

        public decimal? CoverWeight
        {
            get { return this.Entity.GetAttributeValue<decimal?>("new_coverbasepercentage"); }
        }

        public decimal? PremiumAmount
        {
            get { return this.Entity.GetAttributeValue<Money>("new_coverpremium").ToDecimal(); }
        }

        public EntityReference RegulatoryClassRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_regulatoryclassid"); }
        }

        public bool Mandatory
        {
            get { return this.Entity.GetAttributeValue<bool>("new_mandatorycover"); }
        }

        /// <summary>
        /// Peril (Basic Cover) Reference
        /// </summary>
        public EntityReference BasicCoverRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_basiccover"); }
        }
    }
}
