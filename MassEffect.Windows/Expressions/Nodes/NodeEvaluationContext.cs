using System.Diagnostics;

namespace MassEffect.Windows.Expressions.Nodes
{
	internal sealed class NodeEvaluationContext
    {
        public static readonly NodeEvaluationContext Empty = new NodeEvaluationContext(new object[] { });
        private readonly object[] arguments;

        private NodeEvaluationContext(object[] arguments)
        {
            Debug.Assert(arguments != null);
            this.arguments = arguments;
        }

        public static NodeEvaluationContext Create(params object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return Empty;
            }

            return new NodeEvaluationContext(arguments);
        }

        public bool HasArgument(int index)
        {
            Debug.Assert(index >= 0);
            return index < this.arguments.Length;
        }

        public object GetArgument(int index)
        {
            Debug.Assert(index >= 0);
            return this.arguments[index];
        }
    }
}
