using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FubuCore;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Nodes;

namespace FubuMVC.Swagger
{
    public class ApiFinder<T>
    {
        private readonly BehaviorGraph _graph;
        
        public ApiFinder(BehaviorGraph graph)
        {
            _graph = graph;
        }

        public IEnumerable<ActionCall> Actions()
        {
            return _graph.Actions().Where(a => a.InputType().CanBeCastTo<T>()); 
        }
        
        public IEnumerable<IGrouping<string, ActionCall>> ActionsByGroup()
        {
            var partGroupExpression = new Regex(@"^/?api/(?<group>[a-zA-Z0-9_\-\.\+]+)/?");

            //TODO might want to cache the intermediate result

            var groups = Actions()
                .GroupBy(a =>
                             {
                                 var pattern = a.ParentChain().Route.Pattern;
                                 var match = partGroupExpression.Match(pattern);
                                 return match.Success ? match.Groups["group"].Value : null;
                             });
            
            return groups.ToArray();
        }

        public IEnumerable<ActionCall> ActionsForGroup(string name)
        {
            var group = ActionsByGroup().FirstOrDefault(g => g.Key.ToLowerInvariant() == name);

            return @group ?? (IEnumerable<ActionCall>) new ActionCall[0];
        }
    }
}