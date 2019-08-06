using System.Collections.Generic;
using BasLijten.Foundation.Personalization.Rules;
using Sitecore;
using Sitecore.Analytics.Pipelines.GetRenderingRules;
using Sitecore.Analytics.Pipelines.RenderingRuleEvaluated;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using Sitecore.Mvc.Analytics.Pipelines.Response.CustomizeRendering;
using Sitecore.Mvc.Analytics.Presentation;
using Sitecore.Rules;
using Sitecore.Rules.ConditionalRenderings;

namespace BasLijten.Foundation.Personalization.Pipelines
{
    public class FixPersonalize : Personalize
    {
        public override void Process(CustomizeRenderingArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            // the explicit check for "tracker.isactive" has been removed, as it prevents to evaluate conditions which should go off when there is no tracker

            if (args.IsCustomized)
            {
                return;
            }

            this.Evaluate(args);
        }

        protected override void ApplyActions(CustomizeRenderingArgs args, ConditionalRenderingsRuleContext context)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(context, "context");
            RenderingReference renderingReference = context.References.Find((RenderingReference r) => r.UniqueId == context.Reference.UniqueId);
            if (renderingReference == null)
            {
                args.Renderer = new EmptyRenderer();
                return;
            }
            this.ApplyChanges(args.Rendering, renderingReference);
        }

        protected override void Evaluate(CustomizeRenderingArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Item item = args.PageContext.Item;
            if (item == null)
            {
                return;
            }
            RenderingReference renderingReference = CustomizeRenderingProcessor.GetRenderingReference(args.Rendering, Context.Language, args.PageContext.Database);
            GetRenderingRulesArgs expr_3E = new GetRenderingRulesArgs(item, renderingReference);
            GetRenderingRulesPipeline.Run(expr_3E);
            RuleList<ConditionalRenderingsRuleContext> ruleList = expr_3E.RuleList;
            if (ruleList == null || ruleList.Count == 0)
            {
                return;
            }

            // after the ruleList has been retrieved AND ruleList > 0, instantiate a new personalization rulelist which has a fix to continue evaluation of conditions
            // even when some exceptions occur.
            PersonalizationRuleList<ConditionalRenderingsRuleContext> personalizationRuleList = new PersonalizationRuleList<ConditionalRenderingsRuleContext>();
            foreach (var r in ruleList.Rules)
            {
                personalizationRuleList.Add(r);
            }

            ConditionalRenderingsRuleContext conditionalRenderingsRuleContext = new ConditionalRenderingsRuleContext(new List<RenderingReference>
            {
                renderingReference
            }, renderingReference)
            {
                Item = item
            };
            conditionalRenderingsRuleContext.Parameters["mvc.rendering"] = args.Rendering;

            // runRules for the personalizationRuleList
            this.RunRules(personalizationRuleList, conditionalRenderingsRuleContext);
            this.ApplyActions(args, conditionalRenderingsRuleContext);
            args.IsCustomized = true;
        }

        /// <summary>
        /// accept personalizationRuleList, evaluate conditions, 
        /// </summary>
        /// <param name="rules"></param>
        /// <param name="context"></param>
        protected virtual void RunRules(PersonalizationRuleList<ConditionalRenderingsRuleContext> rules, ConditionalRenderingsRuleContext context)
        {
            Assert.ArgumentNotNull(rules, "rules");
            Assert.ArgumentNotNull(context, "context");
            if (!RenderingRuleEvaluatedPipeline.IsEmpty())
            {
                rules.Evaluated += new RuleConditionEventHandler<ConditionalRenderingsRuleContext>(this.RulesEvaluatedHandler);
            }

            rules.RunFirstMatchingIgnoringTrackingExceptions(context);
        }

        private void RulesEvaluatedHandler(RuleList<ConditionalRenderingsRuleContext> ruleList, ConditionalRenderingsRuleContext ruleContext, Rule<ConditionalRenderingsRuleContext> rule)
        {
            RenderingRuleEvaluatedPipeline.Run(new RenderingRuleEvaluatedArgs(ruleList, ruleContext, rule));
        }
    }
}
