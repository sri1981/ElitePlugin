using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    public enum FieldMappingFormat
    {
        SingleLineOfText = 100000000,
        Email = 100000001,
        URL = 100000002,
        OptionSet = 100000003,
        TwoOptions = 100000004,
        WholeNumber = 100000005,
        DecimalNumber = 100000006,
        Currency = 100000007,
        MultipleLinesOfText = 100000008,
        Date = 100000009,
        Lookup = 100000010,
    }

    public enum LookupMapping
    {
        ByName = 100000000,
        ByOptionSet = 100000001,
    }

    public enum AddressOf
    {
        RoleOrPolicyHolder = 100000000,
        InsuredRisk = 100000001
    }

    public enum ColumnValueType
    {
        ColumnMapping = 100000000,
        DefaultValue = 100000001
    }

    sealed public class BordereauTemplateColumn : EntityWrapper
    {
        internal static readonly string FieldMappingAlias = "new_bordereauxfieldmaping";

        public override string LogicalName { get { return "new_bordereauxtemplatecolumn"; } }

        private Entity _fieldMapping;

        public BordereauTemplateColumn(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity)
        {
            var hasLinkedFieldMapping = entity.Attributes
                .Where(a => a.Key.StartsWith(FieldMappingAlias + "."))
                .Any();

            if (!hasLinkedFieldMapping)
            {
                _fieldMapping = RetrieveFieldMapping();
            }
        }

        public int ColumnNumber
        {
            get
            {
                var colNum = this.Entity.GetAttributeValue<int?>("new_columnnumber");
                if (colNum == null)
                    throw new InvalidPluginExecutionException("Column number of field mapping '{0}' is empty. Cannot use mapping.".FormatWith(this.Entity.GetAttributeValue<string>("new_name")));
                return colNum.Value;
            }
        }

        #region Template column fields

        public Guid FieldMappingId
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_bordereauxfieldmaping").Id; }
        }

        public string ColumnLabel
        {
            get { return this.Entity.GetAttributeValue<string>("new_columnlabel"); }
        }

        public bool Mandatory
        {
            get { return this.Entity.GetAttributeValue<bool>("new_mandatory"); }
        }

        public EntityReference CoverRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_cover"); }
        }

        public EntityReference RoleTypeRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_roletype"); }
        }

        public int? RoleNumber
        {
            get { return this.Entity.GetAttributeValue<int?>("new_rolenumber"); }
        }

        public ColumnValueType? ValueType
        {
            get
            {
                var valType = this.Entity.GetAttributeValue<OptionSetValue>("new_valuetype");
                if (valType == null)
                    return null;
                return valType.ToEnum<ColumnValueType>();
            }
        }

        public string DefaultValue
        {
            get { return this.Entity.GetAttributeValue<string>("new_columnvalue"); }
        }

        public EntityReference RiskSubClass
        {
            get 
            { 
                var riskSubClass = this.Entity.GetAttributeValue<EntityReference>("new_risksubclass");
                if (riskSubClass != null)
                    return riskSubClass;

                return null;
                     
            }
        }

        public string CoverId
        {
            get { return this.Entity.GetAttributeValue<string>("new_coverid"); }
        }

        public string PerilId
        {
            get { return this.Entity.GetAttributeValue<string>("new_perilid"); }
        }

        public EntityReference PerilSection
        {
            get
            {
                var perilSection = this.Entity.GetAttributeValue<EntityReference>("new_cover");
                if (perilSection == null)
                    return null;

                return perilSection;
            }
        }

        public EntityReference Peril
        {
            get
            {
                var peril = this.Entity.GetAttributeValue<EntityReference>("new_peril");
                if (peril == null)
                    return null;

                return peril;
            }
        }

        public AddressOf? AddressOf
        {
            get
            {
                var addressOf = this.Entity.GetAttributeValue<OptionSetValue>("new_addressof");

                if (addressOf == null)
                    return null;

                return addressOf.ToEnum<AddressOf>();
            }
        }

        public int? ClaimOrder
        {
            get { return this.Entity.GetAttributeValue<int?>("new_claimorder"); }
        }

        public int? ClaimPaymentOrder
        {
            get { return this.Entity.GetAttributeValue<int?>("new_claimpaymentorder"); }
        }

        public int? ClaimRoleOrder
        {
            get { return this.Entity.GetAttributeValue<int?>("new_claimroleorder"); }
        }

        public int? ClaimRecoveryOrder
        {
            get { return this.Entity.GetAttributeValue<int?>("new_claimrecoveryorder"); }
        }

        public int? ClaimRoleTypeOrder
        {
            get { return this.Entity.GetAttributeValue<int?>("new_claimroletypeorder"); }
        }

        #endregion

        #region Field mapping fields

        public string MappingDisplayName
        {
            get { return GetAttributeFromMapping<string>("new_name"); }
        }

        public string EntityName
        {
            get { return GetAttributeFromMapping<string>("new_destinationentityschemaname"); }
        }

        public string AttributeName
        {
            get { return GetAttributeFromMapping<string>("new_destinationfield"); }
        }

        public string LookupTargetEntityName
        {
            get { return GetAttributeFromMapping<string>("new_lookuptargetentity"); }
        }

        public FieldMappingFormat Format
        {
            get { return GetAttributeFromMapping<OptionSetValue>("new_fieldformat").ToEnum<FieldMappingFormat>(); }
        }

        public LookupMapping LookupMapping
        {
            get
            {
                var mapping = GetAttributeFromMapping<OptionSetValue>("new_lookupmapping");
                if (mapping == null)
                    return Entities.LookupMapping.ByName;

                return mapping.ToEnum<LookupMapping>();
            }
        }

        private IEnumerable<Entity> _optionSets;
        public IEnumerable<Entity> OptionSets
        {
            get
            {
                if (_optionSets != null)
                    return _optionSets;

                _optionSets = RetrieveOptionSets();
                return _optionSets;
            }
        }

        #endregion

        #region mapping-specific retrieval

        public Entity RetrieveLookupByName(string name)
        {
            ThrowIf.Argument.IsNullOrEmpty(name, "name");

            var targetEntity = this.LookupTargetEntityName;
            if (string.IsNullOrEmpty(targetEntity))
                throw new InvalidPluginExecutionException("Field mapping error, lookup target entity is empty.");

            var targets = OrgService.RetrieveMultipleByName(targetEntity, name);
            return targets.FirstOrDefault();
        }

        public Entity RetrieveLookupByAttribute(string attrName, object attrValue)
        {
            ThrowIf.Argument.IsNullOrEmpty(attrName, "attrName");

            var targetEntity = this.LookupTargetEntityName;
            if (string.IsNullOrEmpty(targetEntity))
                throw new InvalidPluginExecutionException("Field mapping error, lookup target entity is empty.");

            var targets = OrgService.RetrieveMultipleByAttribute(targetEntity, attrName, attrValue);
            return targets.FirstOrDefault();
        }

        public Entity RetrieveLookupByOptionSet(string optSetAttribute, int optSetValue)
        {
            ThrowIf.Argument.IsNullOrEmpty(optSetAttribute, "optSetAttribute");

            var targetEntity = this.LookupTargetEntityName;
            if (string.IsNullOrEmpty(targetEntity))
                throw new InvalidPluginExecutionException("Field mapping error, lookup target entity is empty.");

            var query = new QueryExpression(this.LookupTargetEntityName);
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition(optSetAttribute, ConditionOperator.Equal, optSetValue);

            var result = OrgService.RetrieveMultiple(query);
            return result.Entities.FirstOrDefault();
        }

        #endregion

        /// <summary>
        /// Retrieves related field mapping record.
        /// </summary>
        /// <returns></returns>
        private Entity RetrieveFieldMapping()
        {
            var mappingRef = this.Entity.GetAttributeValue<EntityReference>("new_bordereauxfieldmaping");
            var mapping = OrgService.Retrieve(mappingRef);
            return mapping;
        }

        /// <summary>
        /// Get attribute from mapping aliased values with fallback to the mapping entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="attribute"></param>
        /// <returns></returns>
        private T GetAttributeFromMapping<T>(string attribute)
        {
            ThrowIf.Argument.IsNullOrEmpty(attribute, "attribute");

            // first try to get value from link-entity aliased column
            var aliasedAttr = FieldMappingAlias + "." + attribute;
            if (this.Entity.Contains(aliasedAttr))
            {
                var aliasedVal = this.Entity.GetAttributeValue<AliasedValue>(aliasedAttr);
                return aliasedVal.ToValue<T>();
            }

            // fallback to retrieved entity
            if (_fieldMapping != null)
                return _fieldMapping.GetAttributeValue<T>(attribute);

            return default(T);
        }

        private IEnumerable<Entity> RetrieveOptionSets()
        {
            var optionSetTableRef = this.GetAttributeFromMapping<EntityReference>("new_optionset");
            if (optionSetTableRef == null)
                return Enumerable.Empty<Entity>();

            var optQuery = new QueryExpression("new_optionset");
            optQuery.ColumnSet.AllColumns = true;
            optQuery.Criteria.AddCondition("new_optionsettable", ConditionOperator.Equal, optionSetTableRef.Id);

            return OrgService.RetrieveMultiple(optQuery).Entities;
        }

    }
}
