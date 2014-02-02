using System.Collections.Generic;

using Nustache.Core;

namespace Mochify.Simile.Core.Reporting
{
    /// <summary>
    /// This is a b
    /// </summary>
    public class MustacheView : IReportView
    {
        public MustacheView()
        {

        }

        public string GenerateTemplate(string template, IDictionary<string, object> dict)
        {
            var html = Render.StringToString(template, dict);
            return html;
        }
    }
}
