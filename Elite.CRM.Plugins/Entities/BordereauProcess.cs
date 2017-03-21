using Elite.CRM.Plugins.BordereauImport;
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
    enum BordereauProcessStatus
    {
        LoadInProgress = 1,
        ManualImport = 100000000,
        Completed = 100000001,
        ScheduledImport = 100000002,
        ScheduledImportInProgress = 100000004,
        Inactive = 2,
        CompletedWithErrors = 100000003,
    }

    enum BordereauProcessBordereauType
    {
        Policy = 100000000,
        Claim = 100000001,
        Payment = 100000002,
    }

    sealed class BordereauProcess : EntityWrapper
    {
        public override string LogicalName { get { return "new_bordereauxprocess"; } }

        public BordereauProcess(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public BordereauProcessStatus Status
        {
            get { return (BordereauProcessStatus)GetStatusCode(); }
        }

        public string ExternalUser
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_externaluser").Name; }
        }

        public EntityReference BrokerRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_brokerid"); }
        }

        private Entity _broker;
        public Entity Broker
        {
            get
            {
                if (_broker != null)
                    return _broker;

                _broker = OrgService.Retrieve(BrokerRef);
                return _broker;
            }
        }

        public EntityReference MonthlyBordereau
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_monthlybordereau"); }
        }

        public EntityReference RiskClassRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_riskclassid"); }
        }

        public string Name
        {
            get { return this.Entity.GetAttributeValue<string>("new_name"); }
        }

        public BordereauProcessBordereauType? BordereauType
        {
            get { return this.Entity.GetOptionsetAsEnum<BordereauProcessBordereauType>("new_bordereauxtype"); }
        }

        public EntityReference ProductRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_product"); }
        }

        public EntityReference CountryRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_country"); }
        }

        public EntityReference CurrencyRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("transactioncurrencyid"); }
        }

        public Entity Bordereau
        {
            get
            {
                var bxRef = this.Entity.GetAttributeValue<EntityReference>("new_bordereauxid");
                if (bxRef == null)
                    return null;

                return this.OrgService.Retrieve(bxRef);
            }
        }

        public OptionSetValue BordereauxMonth
        {
            get
            {
                return this.Entity.GetAttributeValue<OptionSetValue>("new_bordereauxmonth");
            }
        }

        public string BordereauxYear
        {
            get
            {
                return this.Entity.GetAttributeValue<string>("new_bordereauxyear2");
            }
        }

        private IEnumerable<BordereauImportRow> _rows;
        /// <summary>
        /// Retrieves Bordereau import rows for current Bordereau Process.
        /// </summary>
        public IEnumerable<BordereauImportRow> ImportRows
        {
            get
            {
                if (_rows != null)
                    return _rows;

                _rows = RetrieveImportRows();
                return _rows;
            }
        }

        public EntityReference BordereauTemplateRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_bordereauxtemplate"); }
        }


        /// <summary>
        /// Retrieves Bordereau template. 
        /// </summary>
        public BordereauTemplate BordereauTemplate
        {
            get
            {
                //var broker = this.BrokerRef;
                //var template = RetrieveBordereauTemplate(broker.Id);

                //// fallback, if template is not found a specific broker
                //if (template == null)
                //    template = RetrieveBordereauTemplate(null); // retrieve universal template

                //if (template == null)
                //    return null;

                if (this.BordereauTemplateRef == null)
                    return null;

                var template = this.OrgService.Retrieve(this.BordereauTemplateRef);
                return new BordereauTemplate(OrgService, TracingService, template);
            }
        }

        public Entity CreateErrorRecord(BordereauError err)
        {
            ThrowIf.Argument.IsNull(err, "err");

            var errorRecord = new Entity("new_failedrow");

            errorRecord["new_bordereauxprocessid"] = this.EntityReference;
            errorRecord["new_bordereauxtype"] = this.BordereauType.ToOptionSet();
            errorRecord["new_brokerid"] = this.BrokerRef;
            errorRecord["new_riskclassid"] = this.RiskClassRef;

            errorRecord["new_errowrowr"] = err.RowNumber.ToString(); 
            errorRecord["new_errorrow"] = err.RowNumber;
            
            var errorCode = this.OrgService.RetrieveBordereauErrorCode(err.ErrorType, err.Column, err.ErrorCode);
            if (errorCode != null)
            {
                errorRecord["new_bordereauxerrorcodeid"] = errorCode.ToEntityReference();
                errorRecord["new_row"] = err.ErrorDescription = errorCode.GetAttributeValue<string>("new_errordescription");
                err.ErrorCode = errorCode.GetAttributeValue<string>("new_name");
            }
            else
            {
                errorRecord["new_row"] = err.Value;
            }

            if (err.Column != null)
            {
                if (err.Column.ValueType == null || err.Column.ValueType == ColumnValueType.ColumnMapping)
                    errorRecord["new_errorcolumn"] = err.Column.ColumnNumber;

                errorRecord["new_errorcolumnlabel"] = err.Column.ColumnLabel;
            }
            else
            {
                errorRecord["new_errorcolumnlabel"] = err.ColumnLabel;
            }

            var bordereau = this.Bordereau;

            errorRecord["new_importid"] = bordereau.ToEntityReference();
            errorRecord["new_bordereauxcode"] = bordereau.GetAttributeValue<string>("new_bordereauxcode");
            errorRecord["new_bordereauxyear2"] = this.BordereauxYear;
            errorRecord["new_bordereauxmonth"] = this.BordereauxMonth;

            // identifier for claim/policy, rename (or create different field)
            errorRecord["new_policynumber"] = err.RecordIdentifier;

            errorRecord["new_name"] = err.ToString();

            errorRecord.Id = this.OrgService.Create(errorRecord);

            return errorRecord;
        }

        /// <summary>
        /// Retrieves import rows for current process, but only as entity references without any fields. 
        /// </summary>
        public IEnumerable<EntityReference> RetrieveImportRowRefs(BordereauImportRowStatus? status = null)
        {
            var rowsQuery = new QueryExpression("new_bordereauximportrow");
            rowsQuery.NoLock = true;

            rowsQuery.Criteria.AddCondition("new_bordereauxprocess", ConditionOperator.Equal, this.Id);
            if (status != null)
                rowsQuery.Criteria.AddCondition("statuscode", ConditionOperator.Equal, (int)status.Value);

            var result = OrgService.RetrieveMultiple(rowsQuery);
            return result.Entities.Select(e => e.ToEntityReference());
        }

        private IEnumerable<BordereauImportRow> RetrieveImportRows()
        {
            var rowsQuery = new QueryExpression("new_bordereauximportrow");
            rowsQuery.ColumnSet.AllColumns = true;
            rowsQuery.Criteria.AddCondition("new_bordereauxprocess", ConditionOperator.Equal, this.Id);

            var result = OrgService.RetrieveMultiple(rowsQuery);
            return result.Entities.Select(e => new BordereauImportRow(OrgService, TracingService, e));
        }

        private Entity RetrieveBordereauTemplate(Guid? brokerId)
        {
            var templateQuery = new QueryExpression("new_bordereauxriskclasssettings");
            templateQuery.ColumnSet.AllColumns = true;

            templateQuery.Criteria.AddCondition("new_bordereauxtype", ConditionOperator.Equal, (int)this.BordereauType);
            templateQuery.Criteria.AddCondition("new_riskclassid", ConditionOperator.Equal, this.RiskClassRef.Id);

            if (brokerId == null)
                templateQuery.Criteria.AddCondition("new_broker", ConditionOperator.Null);
            else
                templateQuery.Criteria.AddCondition("new_broker", ConditionOperator.Equal, brokerId.Value);

            templateQuery.Criteria.AddCondition("statuscode", ConditionOperator.Equal, 1); // active

            var result = OrgService.RetrieveMultiple(templateQuery);
            return result.Entities.FirstOrDefault();
        }

        public Entity CreateErrorRecords(BordereauError err, MappedRow mappedRow)
        {
            ThrowIf.Argument.IsNull(err, "err");

            var errorRecord = new Entity("new_failedrow");

            errorRecord["new_bordereauxprocessid"] = this.EntityReference;
            errorRecord["new_bordereauxtype"] = this.BordereauType.ToOptionSet();
            errorRecord["new_brokerid"] = this.BrokerRef;
            errorRecord["new_riskclassid"] = this.RiskClassRef;

            errorRecord["new_errowrowr"] = mappedRow.RowNumber.ToString();//err.RowNumber.ToString();
            errorRecord["new_errorrow"] = mappedRow.RowNumber;

            var errorCode = this.OrgService.RetrieveBordereauErrorCode(err.ErrorType, err.Column, err.ErrorCode);
            if (errorCode != null)
            {
                errorRecord["new_bordereauxerrorcodeid"] = errorCode.ToEntityReference();
                errorRecord["new_row"] = err.ErrorDescription = errorCode.GetAttributeValue<string>("new_errordescription");
                err.ErrorCode = errorCode.GetAttributeValue<string>("new_name");
            }
            else
            {
                errorRecord["new_row"] = err.Value;
            }

            if (err.Column != null)
            {
                if (err.Column.ValueType == null || err.Column.ValueType == ColumnValueType.ColumnMapping)
                    errorRecord["new_errorcolumn"] = err.Column.ColumnNumber;

                errorRecord["new_errorcolumnlabel"] = err.Column.ColumnLabel;
            }
            else
            {
                errorRecord["new_errorcolumnlabel"] = err.ColumnLabel;
            }

            var bordereau = this.Bordereau;

            errorRecord["new_importid"] = bordereau.ToEntityReference();
            errorRecord["new_bordereauxcode"] = bordereau.GetAttributeValue<string>("new_bordereauxcode");
            errorRecord["new_bordereauxyear2"] = this.BordereauxYear;
            errorRecord["new_bordereauxmonth"] = this.BordereauxMonth;

            // identifier for claim/policy, rename (or create different field)
            errorRecord["new_policynumber"] = err.RecordIdentifier;

            if(err.ErrorType == BordereauErrorType.BusinessError)
                errorRecord["new_name"] = err.Message;
            else
                errorRecord["new_name"] = err.ErrorDetails;// .ToString();

            errorRecord.Id = this.OrgService.Create(errorRecord);

            return errorRecord;
        }

    }
}
