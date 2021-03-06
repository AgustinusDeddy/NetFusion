﻿using NetFusion.Base.Entity;
using NetFusion.Base.Scripting;
using NetFusion.Common;
using NetFusion.Common.Extensions.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetFusion.Domain.Roslyn.Core
{
    /// <summary>
    /// Instance of this class is cached by the EntityScriptingService and contains a set of
    /// expression evaluators that have their expressions compiled to delegates upon first
    /// use.  A script is assigned an unique name within the context of an entity.  If
    /// an entity script is tagged with the name 'default', it is applied first followed by
    /// the script with a specified name.
    /// </summary>
    public class ScriptEvaluator
    {
        public const string DEFAULT_SCRIPT_NAME = "default";
        public EntityScript Script { get; }
        public IEnumerable<ExpressionEvaluator> Evaluators { get; private set; }

        public ScriptEvaluator(EntityScript script)
        {
            Check.NotNull(script, nameof(script));
            Script = script;
        }

        public void SetExpressionEvaluators(IEnumerable<ExpressionEvaluator> evaluators)
        {
            Check.NotNull(evaluators, nameof(evaluators));
            Evaluators = evaluators;
        }

        public bool IsDefault => this.Script.Name == DEFAULT_SCRIPT_NAME;

        public async Task ExecuteAsync(object entity)
        {
            Check.NotNull(entity, nameof(entity));

            // The scope that will be used to resolve references made within
            // expressions.  This includes the entity and its set of optional
            // dynamic attributes referenced as _ within scripts..
            Type entityScopeType = typeof(EntityScriptScope<>).MakeGenericType(Script.EntityType);
            object entityScope = entityScopeType.CreateInstance(entity);

            foreach (ExpressionEvaluator evaluator in Evaluators
                .OrderBy(ev => ev.Expression.Sequence))
            {
                object result = await evaluator.Executor(entityScope);

                // Determines if a dynamic calculated attribute and not an assignment to
                // static domain-entity property.  If so update or add the attribute's value.
                if (entity is IAttributedEntity attributedEntity && evaluator.Expression.AttributeName != null)
                {
                    attributedEntity.Attributes.SetValue(evaluator.Expression.AttributeName, result, overrideIfPresent: true);
                }
            }
        }
    }
}
