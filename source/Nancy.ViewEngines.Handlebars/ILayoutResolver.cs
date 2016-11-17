using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nancy.ViewEngines.Handlebars
{
    public interface ILayoutResolver
    {
        ViewLocationResult ResolveLayoutLocatioon(ViewLocationResult forView, IRenderContext renderContext);
    }
}