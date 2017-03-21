using Elite.CRM.Plugins.Entities;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.PremiumCalculation
{
    static class Extensions
    {
        /// <summary>
        /// Filters tax records by regulatory class and orders results by 'valid until' date.
        /// </summary>
        /// <param name="taxes"></param>
        /// <param name="regulatoryClassRef"></param>
        /// <returns></returns>
        public static IEnumerable<Tax> ForRegulatoryClass(this IEnumerable<Tax> taxes, EntityReference regulatoryClassRef)
        {
            return taxes.Where(t => regulatoryClassRef.Equals(t.RegulatoryClassRef))
                        .OrderBy(t => t.ValidUntil, Tax.DateComparer);
        }

        /// <summary>
        /// Filters tax records by basic cover (Peril) and orders results by 'valid until' date.
        /// </summary>
        /// <param name="taxes"></param>
        /// <param name="basicCoverRef"></param>
        /// <returns></returns>
        public static IEnumerable<Tax> ForBasicCover(this IEnumerable<Tax> taxes, EntityReference basicCoverRef)
        {
            return taxes.Where(t => basicCoverRef.Equals(t.BasicCoverRef))
            .OrderBy(t => t.ValidUntil, Tax.DateComparer);
        }

        public static CommissionDefinition CreateDefinition(this Commission commission, decimal amount)
        {
            return new CommissionDefinition()
            {
                CommissionId = commission.Id,
                Amount = amount,
                ParticipantRef = commission.CompanyRef ?? commission.ContactRef,
                RoleTypeId = commission.RoleTypeRef.Id
            };
        }

    }
}
