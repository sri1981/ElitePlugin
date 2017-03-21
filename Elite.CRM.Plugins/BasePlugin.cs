using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    /// <summary>
    /// Base plug-in class, based on CRM SDK toolkit base class.
    /// </summary>
    public abstract class BasePlugin : IPlugin
    {
        protected enum PluginStage
        {
            PreValidation = 10,
            PreOperation = 20,
            MainOperationPreOperation = 30,
            PostOperation = 40,
        }

        protected enum PluginMessage
        {
            AddItem,
            AddListMembers,
            AddMember,
            AddMembers,
            AddPrincipalToQueue,
            AddPrivileges,
            AddProductToKit,
            AddRecurrence,
            AddToQueue,
            AddUserToRecordTeam,
            Assign,
            AssignUserRoles,
            Associate,
            BackgroundSend,
            Book,
            Cancel,
            CheckIncoming,
            CheckPromote,
            Clone,
            Close,
            CopyDynamicListToStatic,
            CopySystemForm,
            Create,
            CreateException,
            CreateInstance,
            Delete,
            DeleteOpenInstances,
            DeliverIncoming,
            DeliverPromote,
            DetachFromQueue,
            Disassociate,
            Execute,
            ExecuteById,
            Export,
            ExportAll,
            ExportCompressed,
            ExportCompressedAll,
            GenerateSocialProfile,
            GrantAccess,
            Handle,
            Import,
            ImportAll,
            ImportCompressedAll,
            ImportCompressedWithProgress,
            ImportWithProgress,
            LockInvoicePricing,
            LockSalesOrderPricing,
            Lose,
            Merge,
            ModifyAccess,
            PickFromQueue,
            Publish,
            PublishAll,
            QualifyLead,
            Recalculate,
            ReleaseToQueue,
            RemoveFromQueue,
            RemoveItem,
            RemoveMember,
            RemoveMembers,
            RemovePrivilege,
            RemoveProductFromKit,
            RemoveRelated,
            RemoveUserFromRecordTeam,
            RemoveUserRoles,
            ReplacePrivileges,
            Reschedule,
            Retrieve,
            RetrieveExchangeRate,
            RetrieveFilteredForms,
            RetrieveMultiple,
            RetrievePersonalWall,
            RetrievePrincipalAccess,
            RetrieveRecordWall,
            RetrieveSharedPrincipalsAndAccess,
            RetrieveUnpublished,
            RetrieveUnpublishedMultiple,
            RetrieveUserQueues,
            RevokeAccess,
            Route,
            RouteTo,
            Send,
            SendFromTemplate,
            SetRelated,
            SetState,
            SetStateDynamicEntity,
            TriggerServiceEndpointCheck,
            UnlockInvoicePricing,
            UnlockSalesOrderPricing,
            Update,
            ValidateRecurrenceRule,
            Win
        }

        private class TraceCapturingService : ITracingService
        {
            private ITracingService _trace;
            private StringBuilder _traceContent = new StringBuilder();

            public TraceCapturingService(ITracingService trace)
            {
                if (trace == null)
                    throw new ArgumentNullException("trace");

                _trace = trace;
            }

            public string TraceContent
            {
                get { return _traceContent.ToString(); }
            }

            public void Trace(string format, params object[] args)
            {
                _traceContent.AppendFormat(format, args);
                _traceContent.AppendLine();
                _trace.Trace(format, args);
            }
        }

        protected class LocalPluginContext
        {
            private TraceCapturingService _capturedTrace;
            
            internal IServiceProvider ServiceProvider { get; private set; }

            internal IOrganizationService OrganizationService { get; private set; }

            internal IPluginExecutionContext PluginExecutionContext { get; private set; }

            internal ITracingService TracingService { get; private set; }

            /// <summary>
            /// Gets pre-image entity with alias 'PreImage'. If no such image exist, throws exception.
            /// </summary>
            internal Entity PreImage
            {
                get
                {
                    if (!this.PluginExecutionContext.PreEntityImages.ContainsKey("PreImage"))
                        throw new Exception("Incorrect plug-in registration: expected pre-image with 'PreImage' alias is not registered.");

                    return this.PluginExecutionContext.PreEntityImages["PreImage"];
                }
            }

            /// <summary>
            /// Gets post-image entity with alias 'PostImage'. If no such image exist, throws exception.
            /// </summary>
            internal Entity PostImage
            {
                get
                {
                    if (!this.PluginExecutionContext.PostEntityImages.ContainsKey("PostImage"))
                        throw new Exception("Incorrect plug-in registration: expected post-image with 'PostImage' alias is not registered.");

                    return this.PluginExecutionContext.PostEntityImages["PostImage"];
                }
            }

            private LocalPluginContext() { }

            internal LocalPluginContext(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                {
                    throw new ArgumentNullException("serviceProvider");
                }

                // Obtain the execution context service from the service provider.
                this.PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                // Obtain the tracing service from the service provider.
                var tracingSvc = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                
                // wrap tracing service with custom wrapper, so we can log tracing message if we need to
                this.TracingService = _capturedTrace = new TraceCapturingService(tracingSvc);

                // Obtain the Organization Service factory service from the service provider
                IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                // Use the factory to generate the Organization Service.
                this.OrganizationService = factory.CreateOrganizationService(this.PluginExecutionContext.UserId);
            }

            internal void Trace(string message, params object[] args)
            {
                if (string.IsNullOrWhiteSpace(message) || this.TracingService == null)
                {
                    return;
                }

                //if (this.PluginExecutionContext == null)
                //{
                this.TracingService.Trace(message, args);
                //}
                //else
                //{
                //    var formattedMessage = message;

                //    if (args != null && args.Length > 0)
                //    {
                //        formattedMessage = string.Format(message, args);
                //    }

                //    this.TracingService.Trace("{0}, Correlation Id: {1}, Initiating User: {2}",
                //                              formattedMessage,
                //                              this.PluginExecutionContext.CorrelationId,
                //                              this.PluginExecutionContext.InitiatingUserId);
                //}
            }

            internal string GetTraceContent()
            {
                return _capturedTrace.TraceContent; 
            }
        }

        private string _childClassName;

        // TODO parameterize trace flag
        private bool _traceException = true;

        /// <summary>
        /// Gets the unsecure configuration passed to plugin via registration of the event.
        /// </summary>
        protected string UnsecureConfig { get; private set; }

        /// <summary>
        /// Gets the secure configuration passed to plugin via registration of the event.
        /// </summary>
        protected string SecureConfig { get; private set; }

        private List<Tuple<int, string, string, Action<LocalPluginContext>>> _registeredEvents;

        /// <summary>
        /// Gets the List of events that the plug-in should fire for. Each List
        /// Item is a <see cref="System.Tuple"/> containing the Pipeline Stage, Message and (optionally) the Primary Entity. 
        /// In addition, the fourth parameter provide the delegate to invoke on a matching registration.
        /// </summary>
        private List<Tuple<int, string, string, Action<LocalPluginContext>>> RegisteredEvents
        {
            get
            {
                if (_registeredEvents == null)
                {
                    _registeredEvents = new List<Tuple<int, string, string, Action<LocalPluginContext>>>();
                }

                return _registeredEvents;
            }
        }

        /// <summary>
        ///  Registers an event to be executed for a specific stage of a message execution for given primary entity. 
        /// </summary>
        /// <param name="stage">Stage of execution.</param>
        /// <param name="message">Message, which will fire an event.</param>
        /// <param name="primaryEntity">Logical name of primary entity. Use null if for any entity.</param>
        /// <param name="func">Delegate function, which is invoked on matching registration.</param>
        protected void RegisterEvent(PluginStage stage, PluginMessage message, string primaryEntity, Action<LocalPluginContext> func)
        {
            RegisteredEvents.Add(new Tuple<int, string, string, Action<LocalPluginContext>>((int)stage, message.ToString(), primaryEntity, func));
        }

        /// <summary>
        /// Gets an image from Entity image collection. Returns null if connection is null or it does not contain entity with provided alias.
        /// </summary>
        /// <param name="collection">Entity image collection to get entity image from.</param>
        /// <param name="alias">Alias of the entity image.</param>
        /// <returns>Entity image.</returns>
        protected Entity GetImage(EntityImageCollection collection, string alias)
        {
            if (string.IsNullOrEmpty(alias))
                throw new ArgumentNullException("alias");

            if (collection == null)
                return null;

            if (!collection.ContainsKey(alias))
                return null;

            return collection[alias];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        protected BasePlugin(string unsecureConfig, string secureConfig)
        {
            _childClassName = this.GetType().ToString();

            this.SecureConfig = secureConfig;
            this.UnsecureConfig = unsecureConfig;
        }

        /// <summary>
        /// Parses configuration in a simple "key=value" format. Each key-value pair is
        /// on a separate line. If keys repeat, then last value will be in dictionary.
        /// Whitespace is trimmed. If key or value start and ends with " (quote mark), 
        /// then quote marks are trimmed as well.
        /// </summary>
        /// <param name="config"></param>
        /// <returns>Dictionary with keys and values from configuration.</returns>
        protected Dictionary<string, string> ParseConfig(string config)
        {
            if (string.IsNullOrEmpty(config))
                return null;
            
            var dict = new Dictionary<string, string>();

            using (var rdr = new StringReader(config))
            {
                string line;
                while ((line = rdr.ReadLine()) != null)
                {
                    var indexOfEqualChar = line.IndexOf('=');
                    if (indexOfEqualChar == -1)
                        continue;

                    var key = line.Substring(0, indexOfEqualChar).Trim();
                    if (key.First() == '"' && key.Last() == '"') // if starts and ends with quotes...
                        key = key.Substring(1, key.Length - 2); // ...remove first and last character

                    var value = line.Substring(indexOfEqualChar + 1).Trim();
                    if (value.First() == '"' && value.Last() == '"') // same as...
                        value = value.Substring(1, value.Length - 2); // ...above

                    dict[key] = value;
                }
            }

            return dict;
        }

        /// <summary>
        /// Executes the plug-in.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <remarks>
        /// For improved performance, Microsoft Dynamics CRM caches plug-in instances. 
        /// The plug-in's Execute method should be written to be stateless as the constructor 
        /// is not called for every invocation of the plug-in. Also, multiple system threads 
        /// could execute the plug-in at the same time. All per invocation state information 
        /// is stored in the context. This means that you should not use global variables in plug-ins.
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            // Construct the Local plug-in context.
            LocalPluginContext localcontext = new LocalPluginContext(serviceProvider);

            localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Entered {0}.Execute()", this._childClassName));

            try
            {
                // Iterate over all of the expected registered events to ensure that the plugin
                // has been invoked by an expected event
                // For any given plug-in event at an instance in time, we would expect at most 1 result to match.
                Action<LocalPluginContext> entityAction =
                    (from a in this.RegisteredEvents
                     where (
                     a.Item1 == localcontext.PluginExecutionContext.Stage &&
                     a.Item2 == localcontext.PluginExecutionContext.MessageName &&
                     (string.IsNullOrWhiteSpace(a.Item3) ? true : a.Item3 == localcontext.PluginExecutionContext.PrimaryEntityName)
                     )
                     select a.Item4).FirstOrDefault();

                if (entityAction != null)
                {
                    localcontext.Trace(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is firing for Entity: {1}, Message: {2}, Correlation Id: {3}, Initiating User: {4}",
                        this._childClassName,
                        localcontext.PluginExecutionContext.PrimaryEntityName,
                        localcontext.PluginExecutionContext.MessageName,
                        localcontext.PluginExecutionContext.CorrelationId,
                        localcontext.PluginExecutionContext.InitiatingUserId));

                    entityAction.Invoke(localcontext);

                    // now exit - if the derived plug-in has incorrectly registered overlapping event registrations,
                    // guard against multiple executions.
                    return;
                }
            }
            catch (FaultException<OrganizationServiceFault> e)
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", e.ToString()));

                if (_traceException)
                {
                    var exceptionInfo = DumpException(e);
                    localcontext.TracingService.Trace(exceptionInfo);
                }

                throw;
            }
            catch (Exception e)
            {
                if (_traceException)
                {
                    var exceptionInfo = DumpException(e);
                    localcontext.TracingService.Trace(exceptionInfo);
                }

                throw;
            }
            finally
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exiting {0}.Execute()", this._childClassName));
            }
        }

        public static string DumpException(Exception ex)
        {
            var builder = new StringBuilder();
            builder.Append(ex.GetType().ToString());

            if (ex.Message != null)
                builder.Append(" : ").Append(ex.Message);

            builder.AppendLine();
            builder.AppendLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                builder.AppendLine("------Inner Exception-----");
                builder.AppendLine(ex.InnerException.GetType().ToString());
                if (ex.InnerException.Message != null)
                    builder.Append(" : ").Append(ex.InnerException.Message);
                builder.AppendLine();
                builder.AppendLine(ex.InnerException.StackTrace);
            }

            return builder.ToString();
        }

    }
}
