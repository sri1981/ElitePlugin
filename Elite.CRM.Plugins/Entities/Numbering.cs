using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    sealed class Numbering : EntityWrapper
    {
        public override string LogicalName { get { return "new_numbering"; } }

        public Numbering(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public int? CurrentNumber
        {
            get { return this.Entity.GetAttributeValue<int?>("new_currentnumber"); }
        }

        public string Prefix
        {
            get { return this.Entity.GetAttributeValue<string>("new_prefix"); }
        }

        public string EntityName
        {
            get { return this.Entity.GetAttributeValue<string>("new_entityname"); }
        }

        public string FieldName
        {
            get { return this.Entity.GetAttributeValue<string>("new_fieldname"); }
        }

        public int? NumberLength
        {
            get { return this.Entity.GetAttributeValue<int?>("new_numberlength"); }
        }

        /// <summary>
        /// Gets a next number and updates numbering record.
        /// </summary>
        /// <returns></returns>
        public string GetNextNumber()
        {
            // get current number
            if (this.CurrentNumber == null)
                throw new NotSupportedException("Auto numbering error: Current number is null.");

            var currentNumber = this.CurrentNumber.Value;
            var nextNumber = currentNumber + 1;
            UpdateNumber(nextNumber);

            // create two parts or auto number: number and prefix
            string numberPart = nextNumber.ToString();
            if (this.NumberLength != null)
                numberPart = numberPart.PadLeft(this.NumberLength.Value, '0');

            return "{0}{1}".FormatWith(this.Prefix, numberPart);
        }

        /// <summary>
        /// Updates Numbering entity with specified number - sets it as a current number. 
        /// Then reloads this.Entity with latest data.
        /// </summary>
        /// <param name="number">Number to set to Numbering entity.</param>
        private void UpdateNumber(int number)
        {
            var newNumbering = new Entity(LogicalName);
            newNumbering.Id = this.Id;
            newNumbering["new_currentnumber"] = number;
            this.OrgService.Update(newNumbering);
            this.Reload();
        }
    }
}
