using Elsa.Alterations.Activities;
using Elsa.Alterations.Core.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

namespace Elsa.Alterations.Workflows;

/// <summary>
/// Executes an alteration plan.
/// </summary>
public class ExecuteAlterationPlanWorkflow : WorkflowBase
{
    internal const string WorkflowDefinitionId = "Elsa.Alterations.ExecuteAlterationPlan";
    
    /// <inheritdoc />
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(WorkflowDefinitionId);
        builder.AsSystemWorkflow();
        var plan = builder.WithInput<AlterationPlanParams>("Plan", "The parameters for the new plan");
        var planId = builder.WithVariable<string>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new SubmitAlterationPlan
                {
                    Params = new(context => context.GetInput<AlterationPlanParams>(plan)!),
                    Result = new(planId)
                },
                new Correlate(planId),
                new GenerateAlterationJobs(planId),
                new DispatchAlterationJobs(planId),
                new AlterationPlanCompleted(planId),
            }
        };
    }
}