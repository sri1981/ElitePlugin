using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    sealed class BordereauTemplate : EntityWrapper
    {
        public override string LogicalName { get { return "new_bordereauxriskclasssettings"; } }

        public BordereauTemplate(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        private IEnumerable<BordereauTemplateColumn> _templateColumns;
        public IEnumerable<BordereauTemplateColumn> TemplateColumns
        {
            get
            {
                if (_templateColumns != null)
                    return _templateColumns;

                _templateColumns = RetrieveTemplateColumns();
                return _templateColumns;
            }
        }

        private RiskClass _riskClass;
        public RiskClass RiskClass
        {
            get
            {
                if (_riskClass != null)
                    return _riskClass;

                var riskClassRef = this.Entity.GetAttributeValue<EntityReference>("new_riskclassid");
                if (riskClassRef == null) 
                    return null;

                var riskClassEntity = OrgService.Retrieve(riskClassRef);
                _riskClass = new RiskClass(OrgService, TracingService, riskClassEntity);

                return _riskClass;
            }
        }

        public int StartingRow
        {
            get { return this.Entity.GetAttributeValue<int>("new_startingrow"); }
        }

        public IEnumerable<Guid> UniqueCoverIDs
        {
            get
            {
                return TemplateColumns
                    .Where(c => c.CoverRef != null)
                    .Select(c => c.CoverRef.Id)
                    .Distinct();
            }
        }

        public IEnumerable<Guid> UniqueRoleTypeIDs
        {
            get
            {
                return TemplateColumns
                    .Where(c => c.RoleTypeRef != null)
                    .Select(c => c.RoleTypeRef.Id)
                    .Distinct();
            }
        }

        /// <summary>
        /// Retrieves Bordereau template columns for current template. Returned template columns 
        /// are linked to their Field mappings (new_fieldmapping).
        /// </summary>
        /// <returns></returns>
        private IEnumerable<BordereauTemplateColumn> RetrieveTemplateColumns()
        {
            var tempColumnQuery = new QueryExpression("new_bordereauxtemplatecolumn");
            tempColumnQuery.ColumnSet.AllColumns = true;

            tempColumnQuery.Criteria.AddCondition("new_bordereauxtemplate", ConditionOperator.Equal, this.Entity.Id);

            var mappingLink = tempColumnQuery.AddLink("new_fieldmapping", "new_bordereauxfieldmaping", "new_fieldmappingid");
            mappingLink.EntityAlias = BordereauTemplateColumn.FieldMappingAlias;
            mappingLink.Columns.AllColumns = true;

            var result = OrgService.RetrieveMultiple(tempColumnQuery);
            return result.Entities.Select(e => new BordereauTemplateColumn(OrgService, TracingService, e));
        }

        // TODO update with new claim stuff
        internal BordereauTemplateColumn GetColumn(string entityName, string attrbuteName, Guid? coverId = null, Guid? roleTypeId = null)
        {
            ThrowIf.Argument.IsNullOrEmpty(entityName, "entityName");
            ThrowIf.Argument.IsNullOrEmpty(attrbuteName, "attrbuteName");

            var columns = this.TemplateColumns.Where(col => col.EntityName == entityName &&
                                                            col.AttributeName == attrbuteName);
            if (coverId != null)
                columns = columns.Where(col => col.CoverRef != null && col.CoverRef.Id == coverId);
            else
                columns = columns.Where(col => col.CoverRef == null);

            if (roleTypeId != null)
                columns = columns.Where(col => col.RoleTypeRef != null && col.RoleTypeRef.Id == coverId);
            else
                columns = columns.Where(col => col.RoleTypeRef == null);

            return columns.FirstOrDefault();
        }
        
    }
}
