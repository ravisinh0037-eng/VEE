using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace Cr4ee.Plugins.ProductSlot
{
    public class CreateSlotsOnModelCreate : IPlugin
    {
        private const int TOTAL_SLOTS = 18;
        private const string ENTITY_NAME = "cr4ee_productslot";
        private const string MODEL_FIELD = "cr4ee_productmodel";
        private const string SLOT_FIELD = "cr4ee_name";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context == null || context.Depth > 1)
                return;

            if (!string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase))
                return;

            if (!context.InputParameters.Contains("Target") ||!(context.InputParameters["Target"] is Entity target))
                return;

            if (!target.Contains(MODEL_FIELD))
                return;

            var modelRef = target.GetAttributeValue<EntityReference>(MODEL_FIELD);
            if (modelRef == null)
                return;

            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                
                if (context.Stage == 20)
                {
                    if (!target.Contains(SLOT_FIELD) || string.IsNullOrWhiteSpace(target.GetAttributeValue<string>(SLOT_FIELD)))
                    {
                        throw new InvalidPluginExecutionException("Slot Number is required and cannot be empty.");
                    }

                    var slotName = target.GetAttributeValue<string>(SLOT_FIELD);

                    var dupQuery = new QueryExpression(ENTITY_NAME)
                    {
                        ColumnSet = new ColumnSet(false)
                    };
                    dupQuery.Criteria.AddCondition(MODEL_FIELD, ConditionOperator.Equal, modelRef.Id);
                    dupQuery.Criteria.AddCondition(SLOT_FIELD, ConditionOperator.Equal, slotName);

                    var dupResult = service.RetrieveMultiple(dupQuery);

                    if (dupResult.Entities.Count > 0)
                    {
                        throw new InvalidPluginExecutionException($"Slot '{slotName}' already exists for the selected model.");
                    }

                    return;
                }

                if (context.Stage == 40)
                {
                    tracing.Trace($"Auto-creating slots for Model: {modelRef.Id}");

                    var query = new QueryExpression(ENTITY_NAME)
                    {
                        ColumnSet = new ColumnSet(SLOT_FIELD)
                    };
                    query.Criteria.AddCondition(MODEL_FIELD, ConditionOperator.Equal, modelRef.Id);

                    var existing = service.RetrieveMultiple(query);

                    var existingSlots = new HashSet<string>();
                    foreach (var e in existing.Entities)
                    {
                        var slot = e.GetAttributeValue<string>(SLOT_FIELD);
                        if (!string.IsNullOrWhiteSpace(slot))existingSlots.Add(slot);
                    }

                    for (int i = 1; i <= TOTAL_SLOTS; i++)
                    {
                        var slotNumber = i.ToString();

                        if (existingSlots.Contains(slotNumber))
                            continue;

                        var slotEntity = new Entity(ENTITY_NAME);
                        slotEntity[SLOT_FIELD] = slotNumber;
                        slotEntity[MODEL_FIELD] = modelRef;

                        service.Create(slotEntity);
                    }
                }
            }
            catch (Exception ex)
            {
                tracing.Trace(ex.ToString());
                throw;
            }
        }
    }
}
