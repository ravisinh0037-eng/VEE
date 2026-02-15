using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class GenerateQuotationOnModelChange : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        ITracingService tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
        IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        IOrganizationService service = factory.CreateOrganizationService(context.UserId);

        tracing.Trace("START GenerateQuotationOnModelChange");

        try
        {
          
            if (context.MessageName != "Create" && context.MessageName != "Update")
            {
                tracing.Trace("Exit: Not Create/Update message");
                return;
            }

            if (context.PrimaryEntityName != "cr4ee_productquotation")
            {
                tracing.Trace("Exit: Not Product Quotation entity");
                return;
            }

            if (!context.InputParameters.Contains("Target"))
            {
                tracing.Trace("Exit: Target missing");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            tracing.Trace("Target entity received");

            if (context.MessageName == "Update" && !target.Contains("cr4ee_productmodel"))
            {
                tracing.Trace("Exit: Update without Model change");
                return;
            }

            EntityReference modelRef = target.GetAttributeValue<EntityReference>("cr4ee_productmodel");

            if (modelRef == null)
            {
                tracing.Trace("Exit: Model is NULL");
                return;
            }

            tracing.Trace("Model selected. ModelId = {0}", modelRef.Id);

            // RECORD ID
            Guid quotationId = context.PrimaryEntityId;
            tracing.Trace("ProductQuotationId = {0}", quotationId);

         
            // DELETE EXISTING RECORDS
            tracing.Trace("Deleting existing GenerateQuotation records...");

            QueryExpression deleteQuery = new QueryExpression("cr4ee_generatequotation")
            {
                ColumnSet = new ColumnSet("cr4ee_generatequotationid")
            };

            deleteQuery.Criteria.AddCondition("cr4ee_productquotation",ConditionOperator.Equal,quotationId);

            EntityCollection existing = service.RetrieveMultiple(deleteQuery);

            tracing.Trace("Existing records found: {0}", existing.Entities.Count);

            foreach (var row in existing.Entities)
            {
                tracing.Trace("Deleting GenerateQuotationId: {0}", row.Id);
                service.Delete("cr4ee_generatequotation", row.Id);
            }

            // FETCH OPTIONS
            tracing.Trace("Fetching Product Options for Model...");

            QueryExpression optionQuery = new QueryExpression("cr4ee_productoption")
            {
                ColumnSet = new ColumnSet(
                    "cr4ee_productoptionid",
                    "cr4ee_slot",
                    "cr4ee_name",
                    "cr4ee_shorttext",
                    "cr4ee_description",
                    "cr4ee_listcost",
                    "cr4ee_listprice",
                    "cr4ee_listgm"
                )
            };

            optionQuery.Criteria.AddCondition(
                "cr4ee_productmodel",
                ConditionOperator.Equal,
                modelRef.Id);

            EntityCollection options = service.RetrieveMultiple(optionQuery);

            tracing.Trace("Product Options retrieved: {0}", options.Entities.Count);

            // CREATE RECORDS
            tracing.Trace("Creating GenerateQuotation records...");

            HashSet<Guid> createdSlots = new HashSet<Guid>();
            int counter = 1;

            foreach (Entity option in options.Entities)
            {
                tracing.Trace("Processing option {0}", option.Id);

                EntityReference slotRef = option.GetAttributeValue<EntityReference>("cr4ee_slot");

                // Skip options without slot
                if (slotRef == null)
                {
                    tracing.Trace("Option has no slot. Skipping.");
                    continue;
                }

                // Prevent duplicate slot creation
                if (createdSlots.Contains(slotRef.Id))
                {
                    tracing.Trace("Duplicate slot detected. Slot already created: {0}",slotRef.Id);
                    continue;
                }

                tracing.Trace("Creating record {0} for SlotId: {1}", counter, slotRef.Id);

                Entity generate = new Entity("cr4ee_generatequotation");

                generate["cr4ee_productquotation"] = new EntityReference("cr4ee_productquotation", quotationId);

                generate["cr4ee_productmodel"] = modelRef;
                generate["cr4ee_slot"] = slotRef;

                Guid createdId = service.Create(generate);
                tracing.Trace("GenerateQuotation created. Id = {0}", createdId);

                // Mark slot as created
                createdSlots.Add(slotRef.Id);
                counter++;
            }

            tracing.Trace("END GenerateQuotationOnModelChange (SUCCESS)");
        }
        catch (Exception ex)
        {
            tracing.Trace("===== ERROR =====");
            tracing.Trace(ex.ToString());

            throw new InvalidPluginExecutionException("Error while generating quotation records. Check Plugin Trace Log.",ex);
        }
    }
}
