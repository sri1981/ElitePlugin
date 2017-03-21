using Elite.CRM.Plugins.ErrorHandling;
using Elite.CRM.Plugins;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace Elite.CRM.Plugins.Entities
{
    public abstract class EntityWrapper
    {
        /// <summary>
        /// CRM Entity which is wrapped.
        /// </summary>
        public Entity Entity { get; private set; }

        /// <summary>
        /// Tracing service for tracing service. Do not use it directly, use Trace method of EntityWrapper instead.
        /// </summary>
        protected ITracingService TracingService;

        /// <summary>
        /// IOrganization service to use for all 
        /// </summary>
        protected IOrganizationService OrgService { get; private set; }

        /// <summary>
        /// Override this in subclass to return hard coded logical name of entity which is wrapped by subclass.
        /// </summary>
        public abstract string LogicalName { get; }

        /// <summary>
        /// ID of entity.
        /// </summary>
        public Guid Id { get { return Entity.Id; } }

        /// <summary>
        /// Entity reference based on this entity.
        /// </summary>
        public EntityReference EntityReference { get { return this.Entity.ToEntityReference(); } }

        /// <summary>
        /// Default constructor for wrapper. Accepts entity, which is being wrapped, IOrganizationService which is used for additional data retrieval
        /// and ITracingService, which is used to log tracing messages.
        /// </summary>
        /// <param name="svc">IOrganizationService for communication with CRM.</param>
        /// <param name="tracingSvc">ITracingService for tracing messages.</param>
        /// <param name="entity">Entity to wrap.</param>
        protected EntityWrapper(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(tracingSvc, "tracingSvc");
            ThrowIf.Argument.IsNull(entity, "entity");

            ThrowIf.Argument.IsNotValid(entity.LogicalName != LogicalName,
                "entity",
                "Entity logical name '{0}' is not correct, expected entity logical name '{1}'.".FormatWith(entity.LogicalName, LogicalName));

            OrgService = svc;
            TracingService = tracingSvc;
            Entity = entity;
        }

        /// <summary>
        /// Traces message using current ITracingService.
        /// </summary>
        /// <param name="message">Message format.</param>
        /// <param name="args">Arguments for message format.</param>
        protected void Trace(string message, params object[] args)
        {
            if (TracingService != null)
                TracingService.Trace(message, args);
        }

        /// <summary>
        /// Gets integer value of 'statuscode' field.
        /// </summary>
        /// <returns>Integer value of 'statuscode' field.</returns>
        protected int GetStatusCode()
        {
            var optSetStatus = Entity.GetAttributeValue<OptionSetValue>("statuscode");
            return optSetStatus.Value;
        }

        /// <summary>
        /// Gets value of 'statuscode' field as enum.
        /// </summary>
        /// <returns>Integer value of 'statuscode' field.</returns>
        public T GetStatusCode<T>()
        {
            var optSetStatus = Entity.GetAttributeValue<OptionSetValue>("statuscode");
            return optSetStatus.ToEnum<T>();
        }
        
        /// <summary>
        /// Reloads underlying entity with latest data from CRM.
        /// </summary>
        public void Reload()
        {
            this.Entity = this.OrgService.Retrieve(this.EntityReference);
        }
    }
}
