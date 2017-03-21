using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    public enum BordereauImportRowStatus
    {
        New = 1,
        Loading = 100000000,
        Completed = 100000001,
        Failed = 100000002,
        Duplicate = 100000003
    }

    sealed class BordereauImportRow : EntityWrapper
    {
        public override string LogicalName { get { return "new_bordereauximportrow"; } }

        public BordereauImportRow(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public BordereauImportRowStatus Status
        {
            get { return GetStatusCode<BordereauImportRowStatus>(); }
        }

        public EntityReference BordereauProcessRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_bordereauxprocess"); }
        }

        public BordereauProcess BordereauProcess
        {
            get
            {
                if (this.BordereauProcessRef == null)
                    return null;

                var bxProcess = this.OrgService.RetrieveNoLock(this.BordereauProcessRef.LogicalName, "new_bordereauxprocessid", this.BordereauProcessRef.Id);
                
                return new BordereauProcess(this.OrgService, this.TracingService, bxProcess);
            }
        }

        public int? RowNumber 
        {
            get { return this.Entity.GetAttributeValue<int?>("new_rownumber"); }
        }

        public string this[int colNum]
        {
            get
            {
                ThrowIf.Argument.IsNotValid(colNum <= 0, "colNum", "Column number must be a positive integer larger than zero.");

                var fieldName = "new_" + colNum.ToString();
                if (this.Entity.Contains(fieldName))
                    return this.Entity.GetAttributeValue<string>(fieldName);

                return null;
            }
        }

        public string this[string colLetters]
        {
            get
            {
                var colNum = Utils.LettersToNumber(colLetters);
                return this[colNum];
            }
        }

    }
}
