using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    class ReinsuranceContract : EntityWrapper
    {
        public override string LogicalName { get { return "new_reinsuranceagreement"; } }

        public ReinsuranceContract(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public IEnumerable<ReinsuranceSection> ReinsuranceSections
        {
            get
            {
                return RetrieveReinsuranceSections();
            }
        }

        /// <summary>
        /// Retrieves reinsurance sections applicable for a specified date, i.e. date is between inception date and expiry date.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<ReinsuranceSection> ReinsuranceSectionsForDate(DateTime date)
        {
            return RetrieveReinsuranceSections(date);
        }

        private IEnumerable<ReinsuranceSection> RetrieveReinsuranceSections(DateTime? date = null)
        {
            var sectionsQuery = new QueryExpression("new_reinscontract");
            sectionsQuery.ColumnSet.AllColumns = true;

            sectionsQuery.Criteria.AddCondition("new_reinsuranceagreement", ConditionOperator.Equal, this.Entity.Id);

            // filter sections by date
            if (date != null)
            {
                sectionsQuery.Criteria.AddCondition("new_inceptiondate", ConditionOperator.OnOrBefore, date.Value);
                sectionsQuery.Criteria.AddCondition("new_expirydate", ConditionOperator.OnOrAfter, date.Value);
            }

            var sections = this.OrgService.RetrieveMultiple(sectionsQuery).Entities;
            return sections.Select(e => new ReinsuranceSection(this.OrgService, this.TracingService, e));
        }

    }
}
