using System;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Rules.ConditionalRenderings;

namespace BasLijten.Foundation.Personalization.Rules
{
    /// <summary>
    /// should only be used for the conditionalrenderings
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PersonalizationRuleList<T> : RuleList<T> where T : ConditionalRenderingsRuleContext
    {        
        public new event RuleConditionEventHandler<T> Evaluated;
        public new event RuleConditionEventHandler<T> Evaluating;
        public new event RuleActionEventHandler<T> Applied;

        public void RunFirstMatchingIgnoringTrackingExceptions(T ruleContext)
        {
            Assert.ArgumentNotNull(ruleContext, "ruleContext");
            int num;
            this.RunIgnoreTrackingExceptions(ruleContext, true, out num);
        }

        // Run Forest Run!
        protected virtual void RunIgnoreTrackingExceptions(T ruleContext, bool stopOnFirstMatching, out int executedRulesCount)
        {
            Assert.ArgumentNotNull(ruleContext, "ruleContext");
            executedRulesCount = 0;
            if (this.Count == 0)
            {
                return;
            }
            using (new LongRunningOperationWatcher(Settings.Profiling.RenderFieldThreshold, "Long running rule set: {0}", new string[]
            {
                this.Name ?? string.Empty
            }))
            {
                foreach (Rule<T> current in this.Rules)
                {
                    if (current.Condition != null)
                    {
                        RuleStack ruleStack = new RuleStack();
                        try
                        {
                            PersonalizationRuleList<T>.RaiseConditionEventHandler(this.Evaluating, this, ruleContext, current);
                            current.Condition.Evaluate(ruleContext, ruleStack);
                            PersonalizationRuleList<T>.RaiseConditionEventHandler(this.Evaluated, this, ruleContext, current);
                        }
                        // only skip when there is a tracker like operation
                        catch (InvalidOperationException invalidOperation)
                        {
                            string arg = (current.Name != null) ? current.Name.ToString() : "Unknown";
                            string arg2 = (current.Condition.UniqueId != null) ? current.Condition.UniqueId.ToString() : "Unknown";

                            if (invalidOperation.Message.Contains("Tracker"))
                            {
                                Log.Warn(String.Format("Tracker.Current not available - rule with name: {0} skipped, condtion item ID: {1}", arg, arg2), invalidOperation, this);
                            }
                            else
                            {
                                Log.Error(string.Format("Evaluation of condition failed. Rule item ID: {0}, condition item ID: {1}", arg, arg2), invalidOperation, this);
                                ruleContext.Abort();
                            }
                        }
                        catch (Exception exception)
                        {
                            string arg = (current.Name != null) ? current.Name.ToString() : "Unknown";
                            string arg2 = (current.Condition.UniqueId != null) ? current.Condition.UniqueId.ToString() : "Unknown";

                            Log.Error(string.Format("Evaluation of condition failed. Rule item ID: {0}, condition item ID: {1}", arg, arg2), exception, this);
                            ruleContext.Abort();
                        }
                        if (ruleContext.IsAborted)
                        {
                            break;
                        }
                        if (ruleStack.Count != 0)
                        {
                            bool flag = (bool)ruleStack.Pop();
                            if (!flag || ruleContext.SkipRule)
                            {
                                ruleContext.SkipRule = false;
                            }
                            else
                            {
                                foreach (RuleAction<T> current2 in current.Actions)
                                {
                                    try
                                    {
                                        current2.Apply(ruleContext);
                                        PersonalizationRuleList<T>.RaiseActionEventHandler(this.Applied, this, ruleContext, current2);
                                    }
                                    catch (Exception exception2)
                                    {
                                        string arg3 = (current.Name != null) ? current.Name : "Unknown";
                                        string arg4 = (current2.UniqueId != null) ? current2.UniqueId.ToString() : "Unknown";
                                        Log.Error(string.Format("Execution of action failed. Rule item ID: {0}, action item ID: {1}", arg3, arg4), exception2, this);
                                        ruleContext.Abort();
                                    }
                                    if (ruleContext.IsAborted)
                                    {
                                        return;
                                    }
                                }
                                executedRulesCount++;
                                if (stopOnFirstMatching)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void RaiseConditionEventHandler(RuleConditionEventHandler<T> eventHandler, RuleList<T> rules, T context, Rule<T> rule)
        {
            Assert.ArgumentNotNull(rules, "rules");
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(rule, "rule");
            if (eventHandler == null)
            {
                return;
            }
            eventHandler(rules, context, rule);
        }

        private static void RaiseActionEventHandler(RuleActionEventHandler<T> eventHandler, RuleList<T> rules, T context, RuleAction<T> action)
        {
            Assert.ArgumentNotNull(rules, "rules");
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(action, "action");
            if (eventHandler == null)
            {
                return;
            }
            eventHandler(rules, context, action);
        }
    }
}
