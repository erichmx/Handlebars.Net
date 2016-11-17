using Nancy.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nancy.ViewEngines.Handlebars
{
    public class DefaultLayoutResolver : ILayoutResolver
    {
        public string LayoutsFolder { get; set; } = "Layouts";
        public string DefaultLayoutName { get; set; } = "Default";

        public ViewLocationResult ResolveLayoutLocatioon(ViewLocationResult forView, IRenderContext renderContext)
        {
            var isAjax = renderContext.Context.Request?.IsAjaxRequest() ?? false;
            if (isAjax)
                return null;
            var layoutLocation = renderContext.LocateView($"{LayoutsFolder}/{DefaultLayoutName}", null);
            return layoutLocation;
        }
    }
}