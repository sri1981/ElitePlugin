using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    enum CommissionLevel
    {
        Agreement = 100000000,
        Product = 100000001,
        CoverSection = 100000002,
    }

    enum CommissionMethod
    {
        Percentage = 100000000,
        Amount = 100000001,
        ProvidedByBroker = 100000002,
    }

    class Commission : EntityWrapper
    {
        public override string LogicalName { get { return "new_commissionsalesdetail"; } }

        public Commission(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public DateTime StartDate
        {
            get { return this.Entity.GetAttributeValue<DateTime>("new_startdate"); }
        }

        public DateTime EndDate
        {
            get { return this.Entity.GetAttributeValue<DateTime>("new_enddate1"); }
        }

        public CommissionLevel Level
        {
            get { return this.Entity.GetAttributeValue<OptionSetValue>("new_commissionlevel").ToEnum<CommissionLevel>(); }
        }

        public CommissionMethod Method
        {
            get { return this.Entity.GetAttributeValue<OptionSetValue>("new_commissionmethod").ToEnum<CommissionMethod>(); }
        }

        public Money CommissionAmount
        {
            get { return this.Entity.GetAttributeValue<Money>("new_commissionamount"); }
        }

        public decimal CommissionPercentage
        {
            get { return this.Entity.GetAttributeValue<decimal>("new_commissionpercentage"); }
        }

        public EntityReference CompanyRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_company"); }
        }

        public EntityReference ContactRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_contact"); }
        }

        public EntityReference RoleTypeRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_roletype"); }
        }

    }
}
