using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Collections.Generic;
using Elite.CRM.Plugins.Meta;
using Elite.CRM.Plugins.BordereauImport;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.Text.RegularExpressions;
using System.Text;

namespace Elite.CRM.Plugins
{
    internal static class Extensions
    {
        //private static EntityMetaCache MetaCache = new EntityMetaCache();

        #region General Utilities

        /// <summary>
        /// Shorthand for String.Format() method.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="args">Array of arguments to use for string formatting.</param>
        /// <returns>Formatted string.</returns>
        public static string FormatWith(this string format, params object[] args)
        {
            ThrowIf.Argument.IsNull(format, "format");

            if (args == null || args.Length == 0)
                return format;
            return string.Format(format, args);
        }

        /// <summary>
        /// Truncates string to specified maximum value. If string is shorter, it's not changed.
        /// </summary>
        /// <param name="str">String to shorten.</param>
        /// <param name="length">Maximum length of string.</param>
        /// <returns>Original string truncated to specified length.</returns>
        public static string LimitLength(this string str, int length)
        {
            if (str == null)
                throw new NullReferenceException();

            if (length < 0)
                throw new ArgumentException("Length cannot be negative.", "length");
            else if (length == 0)
                return string.Empty;
            else if (str.Length <= length)
                return str;
            else
                return str.Substring(0, length);
        }

        /// <summary>
        /// Converts enum to OptionSetValue.
        /// </summary>
        /// <param name="e">Enum to convert to OptionSetValue</param>
        /// <returns>OptionSetValue with integer value of enum.</returns>
        public static OptionSetValue ToOptionSet(this Enum e)
        {
            return new OptionSetValue(Convert.ToInt32(e));
        }

        /// <summary>
        /// Converts OptionSetValue to enum of specified type based on integer value of option set.
        /// </summary>
        /// <typeparam name="T">Type of enum.</typeparam>
        /// <param name="value">OptionSetValue to convert</param>
        /// <returns>Enum value with the same integer value as OptionSetValue.</returns>
        public static T ToEnum<T>(this OptionSetValue value)
        {
            ThrowIf.Argument.IsNull(value, "value");
            return (T)Enum.ToObject(typeof(T), value.Value);
        }

        /// <summary>
        /// Converts AliasedValue to specified type, checks for null. If Aliased value is null or its 
        /// internal value is null, returns default for the type.
        /// </summary>
        /// <typeparam name="T">Type to which convert AliasedValue.</typeparam>
        /// <param name="aliased">AliasedValue to convert.</param>
        /// <returns></returns>
        public static T ToValue<T>(this AliasedValue aliased)
        {
            if (aliased == null)
                return default(T);

            var val = aliased.Value;
            if (val == null)
                return default(T);

            return (T)val;
        }

        /// <summary>
        /// Converts Money value to nullable decimal. 
        /// </summary>
        /// <remarks>
        /// This is utility to avoid some double null checks when working with Money attributes.
        /// </remarks>
        /// <param name="m">Money value to convert.</param>
        /// <returns>Decimal value of money or null, if Money object is null.</returns>
        public static decimal? ToDecimal(this Money m)
        {
            if (m == null)
                return null;

            return m.Value;
        }

        /// <summary>
        /// Splits money (decimal) amount into multiple items based on weights associated with individual items. Any difference between sum
        /// of split values and total (due to rounding errors) is subtracted from the item with largest value.
        /// </summary>
        /// <typeparam name="T">Type of items in collection.</typeparam>
        /// <param name="items">Collection of items among which to split decimal money amount. Each item must contain a weight value.</param>
        /// <param name="value">Decimal amount to split.</param>
        /// <param name="weightSelector">Lambda to select weight of individual items.</param>
        /// <param name="precision">Number of decimal places to which all calculated amounts will be rounded.</param>
        /// <returns>Collection of tuples, where Item1 is object from input collection and Item2 is a split amount assigned to the object.</returns>
        public static IEnumerable<Tuple<T, decimal>> SplitAmount<T>(this IEnumerable<T> items, decimal value, Func<T, decimal?> weightSelector, int precision = 2)
        {
            // round value before 
            var roundedValue = Math.Round(value, precision);

            var results = new List<Tuple<T, decimal>>();
            var splitSum = 0M;
            var weightSum = items.Sum(i => weightSelector(i));

            foreach (var item in items)
            {
                var weight = weightSelector(item);
                if (weight == null)
                    continue;

                var splitValue = Math.Round(roundedValue * weight.Value / weightSum.Value, precision);
                splitSum += splitValue;
                results.Add(new Tuple<T, decimal>(item, splitValue));
            }

            // check for rounding error
            if (splitSum != roundedValue)
            {
                var error = splitSum - roundedValue;

                // find tuple with largest value, this will be affected least by an error 
                var maxTuple = results.Aggregate((i1, i2) => i1.Item2 > i2.Item2 ? i1 : i2);

                // replace old value with corrected value
                results.Remove(maxTuple);
                results.Add(new Tuple<T, decimal>(maxTuple.Item1, maxTuple.Item2 - error));
            }

            return results;
        }


        #endregion

        #region Extensions of IOrg service and Entity

        public static Entity Retrieve(this IOrganizationService svc, EntityReference entRef, params string[] columns)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(entRef, "entRef");

            var colSet = (columns == null || columns.Length == 0) ? new ColumnSet(true) : new ColumnSet(columns);
            return svc.Retrieve(entRef.LogicalName, entRef.Id, colSet);
        }

        public static Entity RetrieveNoLock(this IOrganizationService svc, string logicalName, string idAttributeName, Guid id, params string[] columns)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(logicalName, "logicalName");
            ThrowIf.Argument.IsNullOrEmpty(idAttributeName, "idAttributeName");
            ThrowIf.Argument.IsNotValid(id == Guid.Empty, "id", "Cannot retrieve by empty GUID.");

            var query = new QueryExpression(logicalName);
            query.NoLock = true;
            
            if (columns == null || columns.Length == 0)
                query.ColumnSet.AllColumns = true;
            else
                query.ColumnSet.AddColumns(columns);

            query.Criteria.AddCondition(idAttributeName, ConditionOperator.Equal, id);
            return svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public static IEnumerable<Entity> RetrieveMultipleByName(this IOrganizationService svc, string entityLogicalName, string recordName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);
            // skipping metadata for now...
            var primaryNameField = entityLogicalName.StartsWith("new_") ? "new_name" : "name";

            return RetrieveMultipleByAttribute(svc, entityLogicalName, primaryNameField, recordName);
        }

        public static IEnumerable<Entity> RetrieveMultipleByAttribute(this IOrganizationService svc, string entityLogicalName, string attributeName, object attributeValue)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNullOrEmpty(attributeName, "attributeName");

            var query = new QueryExpression(entityLogicalName);
            query.ColumnSet = new ColumnSet(true);

            if (attributeValue != null)
                query.Criteria.AddCondition(attributeName, ConditionOperator.Equal, attributeValue);
            else
                query.Criteria.AddCondition(attributeName, ConditionOperator.Null);

            return svc.RetrieveMultiple(query).Entities;
        }

        public static IEnumerable<Entity> RetrieveAll(this IOrganizationService svc, QueryExpression query)
        {
            var all = new List<Entity>();

            query.PageInfo.PageNumber = 1;

            // top is not supported for queries with paging cookie
            if (query.TopCount != null)
                query.TopCount = null;

            while (true)
            {
                var response = svc.RetrieveMultiple(query);

                all.AddRange(response.Entities);

                if (!response.MoreRecords)
                    break;

                query.PageInfo.PageNumber += 1;
                query.PageInfo.PagingCookie = response.PagingCookie;
            }

            return all;
        }

        public static T? GetOptionsetAsEnum<T>(this Entity entity, string attribute) where T : struct
        {
            ThrowIf.Argument.IsNull(entity, "entity");
            ThrowIf.Argument.IsNotValid(!typeof(T).IsEnum, "T", "Type argument must be an enum.");

            var optSetVal = entity.GetAttributeValue<OptionSetValue>(attribute);
            if (optSetVal == null)
                return null;

            return optSetVal.ToEnum<T>();
        }

        public static T GetAttributeWithFallback<T>(this Entity entity, string attrName, Entity fallbackEntity)
        {
            if (entity == null)
                throw new NullReferenceException();

            if (entity.Contains(attrName))
                return entity.GetAttributeValue<T>(attrName);
            else if (fallbackEntity != null)
                return fallbackEntity.GetAttributeValue<T>(attrName);

            return default(T);
        }

        #endregion

        // TODO consider moving methods from following region into BordereauTemplate class 
        #region Bordereau template columns processing helpers

        /// <summary>
        /// Filters collection of mapped attributes based on Claim order. 
        /// </summary>
        /// <param name="attrs"></param>
        /// <param name="claimOrder"></param>
        /// <returns></returns>
        public static IEnumerable<MappedAttribute> ForClaimOrder(this IEnumerable<MappedAttribute> attrs, int? claimOrder)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (claimOrder == null && c.TemplateColumn.ClaimOrder == null)
                    return true;
                else if (claimOrder == c.TemplateColumn.ClaimOrder)
                    return true;
                return false;
            });
        }

        public static IEnumerable<MappedAttribute> ForPaymentOrder(this IEnumerable<MappedAttribute> attrs, int? paymentOrder)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (paymentOrder == null && c.TemplateColumn.ClaimPaymentOrder == null)
                    return true;
                else if (paymentOrder == c.TemplateColumn.ClaimPaymentOrder)
                    return true;
                return false;
            });
        }

        public static IEnumerable<MappedAttribute> ForRecoveryOrder(this IEnumerable<MappedAttribute> attrs, int? recoveryOrder)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (recoveryOrder == null && c.TemplateColumn.ClaimRecoveryOrder == null)
                    return true;
                else if (recoveryOrder == c.TemplateColumn.ClaimRecoveryOrder)
                    return true;
                return false;
            });
        }

        public static IEnumerable<MappedAttribute> ForRoleNumber(this IEnumerable<MappedAttribute> attrs, int? roleTypeOrder)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");
            return attrs.Where(c =>
            {
                if (roleTypeOrder == null && c.TemplateColumn.ClaimRoleTypeOrder == null)
                    return true;
                else if (roleTypeOrder == c.TemplateColumn.ClaimRoleTypeOrder)
                    return true;
                return false;
            });
        }

        /// <summary>
        /// Returns entity name of a party (e.g. policy holder) for specified role. If no role is supplied, returns entity party name for 
        /// entire row. 
        /// 
        /// Following check is performed:
        ///     If row contains data for mapping account.name or account.new_BrokerCode, 'account' is returned. 
        ///     Otherwise, if row contains data for contact.lastname or contact.fullname, 'contact' is returned.
        ///     If non of above is true, method returns null.
        /// </summary>
        /// <returns>'account' or 'contact', depending on fields in the import row. Null if neither account or contact information is found in the row.</returns>
        public static string ContactOrAccount(this IEnumerable<MappedAttribute> attrs)
        {
            ThrowIf.Argument.IsNull(attrs, "attrs");

            var accountName = attrs
                .ForEntity("account")
                .ForAttribute("name")
                .FirstOrDefault();

            if (accountName != null && accountName.HasValue)
                return "account";

            var accountCode = attrs
                .ForEntity("account")
                .ForAttribute("new_brokercode")
                .FirstOrDefault();

            if (accountCode != null && accountCode.HasValue)
                return "account";

            var contactLastName = attrs
                .ForEntity("contact")
                .ForAttribute("lastname")
                .FirstOrDefault();

            if (contactLastName != null && contactLastName.HasValue)
                return "contact";

            var contactFullName = attrs
                .ForEntity("contact")
                .ForAttribute("fullname")
                .FirstOrDefault();

            if (contactFullName != null && contactFullName.HasValue)
                return "contact";

            return null;
        }

        private static readonly HashSet<string> IgnoredLookupTargets = new HashSet<string>() { "account", "contact", "new_address", "new_postalcode" };

        /// <summary>
        /// Updates entity object with values from mapped attributes. All attributes where attribute.EntityName != entity.LogicalName are ignored.
        /// </summary>
        /// <param name="entity">Entity object to update.</param>
        /// <param name="attrs">Collection of mapped attributes </param>
        /// <param name="overwriteExisting">Flag to indicate whether overwrite existing attributes on entity. Default=True.</param>
        public static void UpdateWithAttributes(this Entity entity, IEnumerable<MappedAttribute> attrs, bool overwriteExisting = true)
        {
            ThrowIf.Argument.IsNull(entity, "entity");

            if (attrs == null || !attrs.Any())
                return;

            foreach (var a in attrs)
            {
                if (a.EntityName != entity.LogicalName)
                    continue;

                if (!a.HasValue)
                    continue;

                // lookups for some entities are handled differently. Therefore, in batch update, we ignore them. 
                if (a.Format == FieldMappingFormat.Lookup && IgnoredLookupTargets.Contains(a.TemplateColumn.LookupTargetEntityName))
                    continue;

                // all lookups for claims are ignored (for some reason)
                // TODO Ask Sri
                if (a.Format == FieldMappingFormat.Lookup && a.TemplateColumn.EntityName == "new_claim")
                    continue;

                if (overwriteExisting || !entity.Contains(a.AttributeName))
                    entity[a.AttributeName] = a.ConvertedValue;
            }
        }

        #endregion

        #region ClaimMethods
        public static IEnumerable<Entity> RetrieveInsuredRiskForClaim(this IOrganizationService svc, string entityLogicalName, Guid policyId, Guid coverId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);

            var query = new QueryByAttribute(entityLogicalName);
            query.AddAttributeValue("new_policyid", policyId);
            query.AddAttributeValue("new_coverid", coverId);
            //query.ColumnSet.AllColumns = true;
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }
        public static IEnumerable<Entity> RetrieveInsuredRiskForClaim(this IOrganizationService svc, string entityLogicalName, Guid policyId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);

            var query = new QueryExpression(entityLogicalName);

            var link = query.AddLink("new_policy", "new_policyid", "new_policyid");
            link.LinkCriteria.AddCondition("new_policy", ConditionOperator.Equal, policyId);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IEnumerable<Entity> RetrieveInsuredRisk(this IOrganizationService svc, string entityLogicalName, string insuredRisk, Guid riskSubClass)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            //ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);

            var query = new QueryExpression(entityLogicalName);

            query.Criteria.AddCondition("new_name", ConditionOperator.Equal, insuredRisk);
            query.Criteria.AddCondition("new_secondlevelriskclass", ConditionOperator.Equal, riskSubClass);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IEnumerable<Entity> RetrieveInsuredRiskForPolicy(this IOrganizationService svc, Guid policyVersionId, Guid riskId)
        {
            var query = new QueryExpression("new_insuredrisk");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, policyVersionId);
            query.Criteria.AddCondition("new_riskid", ConditionOperator.Equal, riskId);

            return svc.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// Retrieves the claim for Bordereau.
        /// </summary>
        /// <param name="svc">The SVC.</param>
        /// <param name="entityLogicalName">Name of the entity logical.</param>
        /// <param name="claimRef">The claim reference.</param>
        /// <returns></returns>
        public static IList<Entity> RetrieveClaim(this IOrganizationService svc, string entityLogicalName, string claimRef, Guid? brokerId, Guid? lossTypeId, DateTime? dateOfLoss)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);

            var query = new QueryExpression(entityLogicalName);
            query.Criteria.AddCondition("new_claimreference", ConditionOperator.Equal, claimRef);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            query.Criteria.AddCondition("new_losstypeid", ConditionOperator.Equal, lossTypeId);
            query.Criteria.AddCondition("new_dateofloss", ConditionOperator.Equal, dateOfLoss);

            var link = query.AddLink("new_policyfolder", "new_policyid", "new_policyfolderid");
            link.LinkCriteria.AddCondition("new_broker", ConditionOperator.Equal, brokerId);


            query.ColumnSet = new ColumnSet(true);
            query.AddOrder("createdon", OrderType.Descending);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }
        //Retrieve claim for the claim plugin
        public static IList<Entity> RetrieveClaim(this IOrganizationService svc, string entityLogicalName, Guid? policyId, DateTime? dateOfLoss = null)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "svc");

            //var meta = MetaCache.RetrieveEntity(svc, entityLogicalName);

            var query = new QueryExpression(entityLogicalName);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            //query.Criteria.AddCondition("new_losstypeid", ConditionOperator.Equal, lossTypeId);
            if (dateOfLoss != null)
                query.Criteria.AddCondition("new_dateofloss", ConditionOperator.Equal, dateOfLoss);
            query.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, policyId);

            query.ColumnSet = new ColumnSet(true);
            query.AddOrder("createdon", OrderType.Descending);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IList<Entity> RetrieveLossType(this IOrganizationService svc, string entityLogicalName, string lossType)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNullOrEmpty(lossType, "lossType");

            var query = new QueryByAttribute(entityLogicalName);
            query.AddAttributeValue("new_name", lossType);
            //query.AddAttributeValue("new_perillevel", 100000000);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IList<Entity> RetrieveCoveredPeril(this IOrganizationService svc, string entityLogicalName, Guid riskClassId, Guid subPerilId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(riskClassId, "riskClassId");
            ThrowIf.Argument.IsNull(subPerilId, "subPerilId");

            QueryExpression query = new QueryExpression(entityLogicalName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_riskclass", ConditionOperator.Equal, riskClassId);
            query.Criteria.AddCondition("new_subperil", ConditionOperator.Equal, subPerilId);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;

        }

        public static IList<Entity> RetrievePolicyVersion(this IOrganizationService svc, string entityLogicalName, Guid policyId, DateTime? lossDate, Guid coverId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(policyId, "policyId");
            ThrowIf.Argument.IsNull(lossDate, "lossDate");

            var query = new QueryExpression(entityLogicalName);
            query.Criteria.AddCondition("new_policy", ConditionOperator.Equal, policyId);
            query.Criteria.AddCondition("new_endofcover", ConditionOperator.OnOrAfter, lossDate);
            query.Criteria.AddCondition("new_commencementofcover", ConditionOperator.OnOrBefore, lossDate);
            query.Criteria.AddCondition("statuscode", ConditionOperator.NotEqual, 100000002);


            var link = query.AddLink("new_insuredcover", "new_policyid", "new_policyid");
            link.LinkCriteria.AddCondition("new_coverid", ConditionOperator.Equal, coverId);
            link.Columns = new ColumnSet("new_insuredcoverid");

            query.AddOrder("createdon", OrderType.Descending);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }

        public static Entity RetrivePolicyVersionBasedOnPolicy(this IOrganizationService svc, Guid policyFolderId, DateTime? lossDate)
        {
            var query = new QueryExpression("new_policy");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_policy", ConditionOperator.Equal, policyFolderId);
            query.Criteria.AddCondition("new_endofcover", ConditionOperator.OnOrAfter, lossDate);
            query.Criteria.AddCondition("new_commencementofcover", ConditionOperator.OnOrBefore, lossDate);
            query.Criteria.AddCondition("statuscode", ConditionOperator.NotEqual, 100000002);
            query.AddOrder("createdon", OrderType.Descending);

            return svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public static IEnumerable<Entity> RetriveInsuredRisk(this IOrganizationService svc, Guid policyVersion, Guid productId, Guid riskSubClassId)
        {
            var query = new QueryExpression("new_insuredrisk");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, policyVersion);
            query.Criteria.AddCondition("new_product", ConditionOperator.Equal, productId);
            query.Criteria.AddCondition("new_secondlevelriskclass", ConditionOperator.Equal, riskSubClassId);

            return svc.RetrieveMultiple(query).Entities;
        }

        public static IList<Entity> RetrieveCover(this IOrganizationService svc, string entityLogicalName, Guid basicCoverId, Guid productId, Guid riskObjectId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(basicCoverId, "basicCoverId");
            ThrowIf.Argument.IsNull(productId, "productId");

            var query = new QueryByAttribute(entityLogicalName);
            query.AddAttributeValue("new_basiccover", basicCoverId);
            query.AddAttributeValue("new_productid", productId);
            query.AddAttributeValue("new_riskid", riskObjectId);
            //query.AddAttributeValue("statecode", 0);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IList<Entity> RetrieveRiskObject(this IOrganizationService svc, Guid riskClassId, Guid riskSubClassId, Guid productId)
        {
            var query = new QueryExpression("new_risk");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_firstlevelriskclassid", ConditionOperator.Equal, riskClassId);
            query.Criteria.AddCondition("new_secondlevelriskclassid", ConditionOperator.Equal, riskSubClassId);
            query.Criteria.AddCondition("new_productid", ConditionOperator.Equal, productId);

            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static Entity RetrieveClaimPaymentOrRecovery(this IOrganizationService svc, string entityName, Guid claimId)
        {
            var query = new QueryExpression(entityName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_claim", ConditionOperator.Equal, claimId);
            query.AddOrder("createdon", OrderType.Descending);
            
            var result = svc.RetrieveMultiple(query);

            return result.Entities.FirstOrDefault();
        }

        public static IList<Entity> RetrieveInsuredCover(this IOrganizationService svc, string entityLogicalName, Guid policyId, Guid coverId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(policyId, "policyId");
            //ThrowIf.Argument.IsNull(coverId, "coverId");

            var query = new QueryExpression(entityLogicalName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_coverid", ConditionOperator.Equal, coverId);
            query.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, policyId);

            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }

        public static IList<Entity> RetrieveInsuredCoversForInsuredRisk(this IOrganizationService svc, Guid insuredRiskId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNotValid(insuredRiskId == Guid.Empty, "insuredRiskId", "insuredRiskId cannot be empty GUID.");

            var query = new QueryExpression("new_insuredcover");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_insuredriskid", ConditionOperator.Equal, insuredRiskId);

            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }

        public static IList<Entity> RetrieveSubPeril(this IOrganizationService svc, string subPeril)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            //ThrowIf.Argument.IsNotValid(insuredRiskId == Guid.Empty, "insuredRiskId", "insuredRiskId cannot be empty GUID.");

            var query = new QueryExpression("new_losstype");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_name", ConditionOperator.Equal, subPeril);
            //query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 1);
            //query.Criteria.AddCondition("new_perillevel", ConditionOperator.Equal, 100000001);

            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }

        //Do we need this function? can we use RetrieveMultipleByName instead?
        public static IList<Entity> RetriveClaimRelatedEntity(this IOrganizationService svc, string entityLogicalName, string claimReference)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");

            var query = new QueryByAttribute(entityLogicalName);
            query.AddAttributeValue("new_name", claimReference);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static IList<Entity> RetrieveClaimTransactionsorPayments(this IOrganizationService svc, string entityLogicalName, Guid claimId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(claimId, "claimId");

            var query = new QueryExpression(entityLogicalName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_claim", ConditionOperator.Equal, claimId);
            query.AddOrder("createdon", OrderType.Descending);

            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }
                
        #endregion

        public static IList<Entity> RetrieveRisk(this IOrganizationService svc, string entityLogicalName, Guid policyVersionId, Guid productId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityLogicalName, "entityLogicalName");
            ThrowIf.Argument.IsNull(policyVersionId, "policyVersionId");

            var query = new QueryByAttribute(entityLogicalName);
            query.AddAttributeValue("new_policyid", policyVersionId);
            query.AddAttributeValue("new_product", productId);
            query.ColumnSet = new ColumnSet(true);
            var result = svc.RetrieveMultiple(query);

            return result.Entities;
        }

        public static Entity RetriveRiskSubClassByName(this IOrganizationService svc, string entityLogicalName, string riskSubClass)
        {
            var query = new QueryExpression(entityLogicalName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_name", ConditionOperator.Equal, riskSubClass);
            query.Criteria.AddCondition("new_riskclasslevel", ConditionOperator.Equal, 100000001);

            return svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public static IList<Entity> RetriveRiskBasedOnSubClass(this IOrganizationService svc, Guid riskSubClassId, Guid new_productid)
        {
            var query = new QueryExpression("new_risk");
            query.Criteria.AddCondition("new_secondlevelriskclassid", ConditionOperator.Equal, riskSubClassId);
            query.Criteria.AddCondition("new_productid", ConditionOperator.Equal, new_productid);
            query.ColumnSet.AllColumns = true;

            return svc.RetrieveMultiple(query).Entities;
        }

        public static Entity RetrieveAccountByName(this IOrganizationService svc, string accountName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(accountName, "accountName");

            var query = new QueryExpression("account");
            query.Criteria.AddCondition("name", ConditionOperator.Equal, accountName);
            query.ColumnSet.AllColumns = true;

            var result = svc.RetrieveMultiple(query);

            return result.Entities.FirstOrDefault();
        }

        public static IEnumerable<Entity> RetrieveContactByName(this IOrganizationService svc, string firstName, string lastName, string postalCode = null, DateTime? dateOfBirth = null, string email = null, string mobile = null, string niNumber = null)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(firstName, "firstName");
            ThrowIf.Argument.IsNullOrEmpty(lastName, "lastName");
            //ThrowIf.Argument.IsNullOrEmpty(postalCode, "postalCode");

            var query = new QueryExpression("contact");
            query.Criteria.AddCondition("firstname", ConditionOperator.Equal, firstName);
            query.Criteria.AddCondition("lastname", ConditionOperator.Equal, lastName);

            if (postalCode != null)
            {
                var postCodeLink = query.AddLink("new_postalcode", "new_postalcode", "new_postalcodeid");
                postCodeLink.LinkCriteria.AddCondition("new_name", ConditionOperator.Equal, postalCode);
            }
            else
            {
                return Enumerable.Empty<Entity>();
            }

            if (dateOfBirth != null)
                query.Criteria.AddCondition("birthdate", ConditionOperator.On, dateOfBirth.Value.Date);
            else
                query.Criteria.AddCondition("birthdate", ConditionOperator.Null);

            if (email != null)
                query.Criteria.AddCondition("emailaddress1", ConditionOperator.Equal, email);
            else
                query.Criteria.AddCondition("emailaddress1", ConditionOperator.Null);


            if (mobile != null)
                query.Criteria.AddCondition("mobilephone", ConditionOperator.Equal, mobile);
            else
                query.Criteria.AddCondition("mobilephone", ConditionOperator.Null);

            if (niNumber != null)
                query.Criteria.AddCondition("new_clientninumber", ConditionOperator.Equal, niNumber);
            else
                query.Criteria.AddCondition("new_clientninumber", ConditionOperator.Null);

            query.ColumnSet.AllColumns = true;

            var result = svc.RetrieveMultiple(query);
            return result.Entities;
        }

        public static Entity RetrieveContactByEmail(this IOrganizationService svc, string email)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(email, "email");

            var query = new QueryExpression("contact");
            query.Criteria.AddCondition("emailaddress1", ConditionOperator.Equal, email);

            query.ColumnSet.AllColumns = true;

            var result = svc.RetrieveMultiple(query);
            return result.Entities.FirstOrDefault();
        }

        public static Entity RetrieveContactByPhone(this IOrganizationService svc, string phoneNumber)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(phoneNumber, "phoneNumber");

            var query = new QueryExpression("contact");
            query.Criteria.AddCondition("telephone1", ConditionOperator.Equal, phoneNumber);

            query.ColumnSet.AllColumns = true;

            var result = svc.RetrieveMultiple(query);
            return result.Entities.FirstOrDefault();
        }

        /// <summary>
        /// This function is used to retrieve the optionset value using the optionset text label
        /// </summary>
        /// <param name="service"></param>
        /// <param name="entityName"></param>
        /// <param name="attributeName"></param>
        /// <param name="selectedLabel"></param>
        /// <returns></returns>
        public static int GetOptionsSetValueForLabel(this IOrganizationService svc, string entityName, string attributeName, string selectedLabel)
        {
            ThrowIf.Argument.IsNullOrEmpty(entityName, "entityName");
            ThrowIf.Argument.IsNullOrEmpty(attributeName, "attributeName");
            //ThrowIf.Argument.IsNullOrEmpty(selectedLabel, "selectedLabel");

            if (selectedLabel != null)
            {
                RetrieveAttributeRequest retrieveAttributeRequest = new
                RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = attributeName,
                    RetrieveAsIfPublished = true
                };
                // Execute the request.
                RetrieveAttributeResponse retrieveAttributeResponse = (RetrieveAttributeResponse)svc.Execute(retrieveAttributeRequest);
                // Access the retrieved attribute.
                Microsoft.Xrm.Sdk.Metadata.PicklistAttributeMetadata retrievedPicklistAttributeMetadata = (Microsoft.Xrm.Sdk.Metadata.PicklistAttributeMetadata)
                retrieveAttributeResponse.AttributeMetadata;// Get the current options list for the retrieved attribute.
                OptionMetadata[] optionList = retrievedPicklistAttributeMetadata.OptionSet.Options.ToArray();
                int selectedOptionValue = 0;
                foreach (OptionMetadata oMD in optionList)
                {
                    if (oMD.Label.LocalizedLabels[0].Label.ToString().ToLower() == selectedLabel.ToLower())
                    {
                        selectedOptionValue = oMD.Value.Value;
                        break;
                    }
                }
                return selectedOptionValue;
            }
            else
                return -1;
        }

        public static string GetOptionSetValueLabel(this IOrganizationService svc, string entityName, string fieldName, int optionSetValue)
        {
            ThrowIf.Argument.IsNullOrEmpty(entityName, "entityName");
            ThrowIf.Argument.IsNullOrEmpty(fieldName, "fieldName");
            ThrowIf.Argument.IsNull(optionSetValue, "optionSetValue");

            var attReq = new RetrieveAttributeRequest();
            attReq.EntityLogicalName = entityName;
            attReq.LogicalName = fieldName;
            attReq.RetrieveAsIfPublished = true;

            var attResponse = (RetrieveAttributeResponse)svc.Execute(attReq);
            var attMetadata = (EnumAttributeMetadata)attResponse.AttributeMetadata;

            return attMetadata.OptionSet.Options.Where(x => x.Value == optionSetValue).FirstOrDefault().Label.UserLocalizedLabel.Label;

        }

        public static int GetOptionsSetValueForStatus(this IOrganizationService svc, string entityName, string attributeName, string selectedLabel)
        {
            ThrowIf.Argument.IsNullOrEmpty(entityName, "entityName");
            ThrowIf.Argument.IsNullOrEmpty(attributeName, "attributeName");
            ThrowIf.Argument.IsNullOrEmpty(selectedLabel, "selectedLabel");

            RetrieveAttributeRequest retrieveAttributeRequest = new
            RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = true
            };
            // Execute the request.
            RetrieveAttributeResponse retrieveAttributeResponse = (RetrieveAttributeResponse)svc.Execute(retrieveAttributeRequest);
            // Access the retrieved attribute.
            Microsoft.Xrm.Sdk.Metadata.StatusAttributeMetadata retrievedPicklistAttributeMetadata = (Microsoft.Xrm.Sdk.Metadata.StatusAttributeMetadata)
            retrieveAttributeResponse.AttributeMetadata;// Get the current options list for the retrieved attribute.
            OptionMetadata[] optionList = retrievedPicklistAttributeMetadata.OptionSet.Options.ToArray();
            int selectedOptionValue = 0;
            foreach (OptionMetadata oMD in optionList)
            {
                if (oMD.Label.LocalizedLabels[0].Label.ToString().ToLower() == selectedLabel.ToLower())
                {
                    selectedOptionValue = oMD.Value.Value;
                    break;
                }
            }
            return selectedOptionValue;
        }

        public static Entity MatchAddress(this IOrganizationService svc, Guid postalCodeId, string street, string number, string buildingDetails)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNotValid(postalCodeId == Guid.Empty, "postalCodeId", "Cannot match address without postalcode.");

            if (string.IsNullOrEmpty(street))
                return null;

            var addrQuery = new QueryExpression("new_address");
            addrQuery.Criteria.AddCondition("new_postalcode", ConditionOperator.Equal, postalCodeId);
            addrQuery.Criteria.AddCondition("new_street1", ConditionOperator.Equal, street);

            if (string.IsNullOrEmpty(number))
                addrQuery.Criteria.AddCondition("new_addressnumbertext", ConditionOperator.Null);
            else 
                addrQuery.Criteria.AddCondition("new_addressnumbertext", ConditionOperator.Equal, number);

            if (string.IsNullOrEmpty(buildingDetails))
                addrQuery.Criteria.AddCondition("new_addressname", ConditionOperator.Null);
            else
                addrQuery.Criteria.AddCondition("new_addressname", ConditionOperator.Equal, buildingDetails);

            return svc.RetrieveMultiple(addrQuery).Entities.FirstOrDefault();
        }

        /// <summary>
        /// Searches for existing postal code from CRM, searches for record by new_codeforsearch.
        /// </summary>
        /// <param name="postalCode">Postal code for search.</param>
        /// <param name="countryId">Id of country, for which we search for postal code.</param>
        /// <param name="ignoreRecordId">Id of a postal code record, which should be ignored</param>
        /// <returns></returns>
        public static Entity SearchPostalCode(this IOrganizationService svc, string postalCode, Guid countryId, Guid? ignoreRecordId = null)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(postalCode, "postalCode");
            ThrowIf.Argument.IsNotValid(countryId == Guid.Empty, "countryId", "countryId cannot be empty GUID.");

            // to lowercase, remove all whitespace and dashes
            var normalizedPostalCode = Utils.NormalizePostalCode(postalCode);

            var pcQuery = new QueryExpression("new_postalcode");
            pcQuery.ColumnSet.AllColumns = true;
            pcQuery.TopCount = 1;

            pcQuery.Criteria.AddCondition("new_codeforsearch", ConditionOperator.Equal, normalizedPostalCode);
            pcQuery.Criteria.AddCondition("new_country", ConditionOperator.Equal, countryId);
            pcQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, (int)CustomEntityStatus.Active);

            if (ignoreRecordId != null)
                pcQuery.Criteria.AddCondition("new_postalcodeid", ConditionOperator.NotEqual, ignoreRecordId.Value);

            var results = svc.RetrieveMultiple(pcQuery);

            return results.Entities.FirstOrDefault();
        }

        /// <summary>
        /// Creates or updates an entity. If creating entity, argument's Id field is set to ID of newly created record.
        /// </summary>
        /// <param name="svc"></param>
        /// <param name="entity"></param>
        public static void CreateOrUpdateEntity(this IOrganizationService svc, ref Entity entity) // TODO refactor
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(entity, "entity");

            if (entity.Id == Guid.Empty)
                entity.Id = svc.Create(entity);
            else
                svc.Update(entity);
        }

        public static IEnumerable<Entity> RetrieveClaimForClaimFolder(this IOrganizationService svc, Guid claimFolderId)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(claimFolderId, "claimFolderId");

            QueryExpression query = new QueryExpression("new_claim");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_claimfolder", ConditionOperator.Equal, claimFolderId);

            return svc.RetrieveMultiple(query).Entities;
        }

        public static Entity RetrieveBordereauErrorCode(this IOrganizationService svc, BordereauErrorType errorType, BordereauTemplateColumn templateColumn, string errorCode = null)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            //ThrowIf.Argument.IsNull(templateColumn, "templateColumn");

            var query = new QueryExpression("new_errorcode");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_errortype", ConditionOperator.Equal, (int)errorType);

            if (errorType == BordereauErrorType.MissingValue)
            {
                errorCode = "E-V0001";
            }
            else if (errorType == BordereauErrorType.IncorrectFormat)
            {
                switch (templateColumn.Format)
                {
                    case FieldMappingFormat.Date:
                        errorCode = "E-F0001";
                        break;
                    case FieldMappingFormat.Currency:
                        errorCode = "E-F0002";
                        break;
                    case FieldMappingFormat.WholeNumber:
                    case FieldMappingFormat.DecimalNumber:
                        errorCode = "E-F0003";
                        break;
                    case FieldMappingFormat.Email:
                        errorCode = "E-F0004";
                        break;
                    case FieldMappingFormat.OptionSet:
                    case FieldMappingFormat.TwoOptions:
                    case FieldMappingFormat.Lookup:
                        errorCode = "E-F0005";
                        break;
                    default:
                        errorCode = "E-F0006";
                        break;
                }
            }

            if (errorCode != null)
                query.Criteria.AddCondition("new_name", ConditionOperator.Equal, errorCode);

            var codes = svc.RetrieveMultiple(query).Entities;

            if (codes.Count < 2)
                return codes.FirstOrDefault();

            // return null if 2 or more codes are found - might be ambiguity or wrong configuration.
            return null;
        }

        public static Entity RetrievePerilSection(this IOrganizationService svc, Guid perilId, Guid riskId, Guid productId)
        {
            QueryExpression query = new QueryExpression("new_cover");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_productid", ConditionOperator.Equal, productId);
            query.Criteria.AddCondition("new_basiccover", ConditionOperator.Equal, perilId);
            query.Criteria.AddCondition("new_riskid", ConditionOperator.Equal, riskId);

            return svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public static Entity RetrieveMonthlyBordereau(this IOrganizationService svc, BordereauProcess process)
        {
            QueryExpression query = new QueryExpression("new_monthlybordereau");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_month", ConditionOperator.Equal, process.BordereauxMonth.Value);
            query.Criteria.AddCondition("new_year", ConditionOperator.Equal, process.BordereauxYear);
            query.Criteria.AddCondition("new_broker", ConditionOperator.Equal, process.BrokerRef.Id);
            query.Criteria.AddCondition("new_bordereautype", ConditionOperator.Equal, (int)process.BordereauType.Value);

            return svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public static int RetrieveApprovalsPerUser(this IOrganizationService svc, string entityName, Guid entityId, Guid userId)
        {
            QueryExpression query = new QueryExpression("new_approval");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition(entityName, ConditionOperator.Equal, entityId);
            query.Criteria.AddCondition("ownerid", ConditionOperator.Equal, userId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            return svc.RetrieveMultiple(query).Entities.Count;
                
        }

        public static bool CheckUser(this IOrganizationService svc, Guid userId, Guid teamId)
        {
            QueryExpression query = new QueryExpression("teammembership");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("teamid", ConditionOperator.Equal, teamId);

            var teams = svc.RetrieveMultiple(query).Entities;

            foreach(var team in teams)
            {
                if (team.GetAttributeValue<Guid>("systemuserid") == userId)
                    return true;

            }

            return false;

        }

        public static int RetriveApprovalCount(this IOrganizationService svc, string entityName, Guid entityId)
        {
            QueryExpression query = new QueryExpression("new_approval");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition(entityName, ConditionOperator.Equal, entityId);
            query.Criteria.AddCondition("new_approvalaction", ConditionOperator.Equal, 100000000);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            return svc.RetrieveMultiple(query).Entities.Count;
        }

        public static int RetriveRejectionCounts(this IOrganizationService svc, string attributeName, Guid attributeId)
        {
            QueryExpression query = new QueryExpression("new_approval");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition(attributeName, ConditionOperator.Equal, attributeId);
            query.Criteria.AddCondition("new_approvalaction", ConditionOperator.Equal, 100000002);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            return svc.RetrieveMultiple(query).Entities.Count;
        }

        public static IEnumerable<Entity> RetrieveTeams(this IOrganizationService svc, int entity)
        {
            QueryExpression query = new QueryExpression("team");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("new_approvalentity", ConditionOperator.Equal, entity);
            query.AddOrder("new_aprovallimit", OrderType.Ascending);

            return svc.RetrieveMultiple(query).Entities;
        }

        #region Debugging utilities

        /// <summary>
        /// Creates a string with entity information.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        internal static string Dump(this Entity e)
        {
            if (e == null)
                throw new NullReferenceException();

            var sb = new StringBuilder(256);
            sb.Append("Logical Name: ").AppendLine(e.LogicalName);
            sb.Append("Id: ").Append(e.Id).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Attributes");

            foreach (var attr in e.Attributes.OrderBy(a => a.Key))
            {
                var formatted = e.FormattedValues.Contains(attr.Key) ? e.FormattedValues[attr.Key] : null;
                var value = attr.Value;

                sb.Append(" ").Append(attr.Key).Append(": ");

                // value
                if (value is Money)
                {
                    sb.Append((value as Money).Value);
                }
                else if (value is string)
                {
                    sb.Append("\"").Append(value).Append("\"");
                }
                else if (value is DateTime)
                {
                    sb.AppendFormat("{0:yyyy-MM-dd HH:mm:ss zzz}", value);
                }
                else if (value is OptionSetValue)
                {
                    sb.AppendFormat("{0:N}", (value as OptionSetValue).Value);
                }
                else if (value is EntityReference)
                {
                    var eref = value as EntityReference;
                    sb.Append(eref.Id);
                    if (!string.IsNullOrEmpty(eref.Name))
                        sb.Append(" \"").Append((value as EntityReference).Name).Append("\"");
                }
                else
                    sb.Append(value);

                if (formatted != null)
                    sb.Append(", (").Append(formatted).Append(")");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
