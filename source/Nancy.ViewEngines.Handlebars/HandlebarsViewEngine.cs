using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;
using Nancy.Responses;
using Nancy.TinyIoc;
using Nancy.ViewEngines;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nancy.ViewEngines.Handlebars
{
    /// <summary>
    /// View engine for rendering Handlebars views.
    /// </summary>
    public class HandlebarsViewEngine : IViewEngine
    {
        /// <summary>
        /// Gets the extensions file extensions that are supported by the view engine.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}"/> instance containing the extensions.</value>
        /// <remarks>The extensions should not have a leading dot in the name.</remarks>
        public IEnumerable<string> Extensions
        {
            get { return new[] { "handlebars" }; }
        }

        /// <summary>
        /// Initialise the view engine (if necessary)
        /// </summary>
        /// <param name="viewEngineStartupContext">Startup context</param>
        public void Initialize(ViewEngineStartupContext viewEngineStartupContext)
        {
        }

        /// <summary>
        /// Renders the view.
        /// </summary>
        /// <param name="viewLocationResult">A <see cref="ViewLocationResult"/> instance, containing information on how to get the view template.</param>
        /// <param name="model">The model that should be passed into the view</param>
        /// <param name="renderContext"></param>
        /// <returns>A response</returns>
        public Response RenderView(ViewLocationResult viewLocationResult, dynamic model, IRenderContext renderContext)
        {
            return new HtmlResponse
            {
                Contents = stream =>
                {
                    var compiledView = this.GetOrCompileTemplate(viewLocationResult, renderContext);

                    var writer = new StreamWriter(stream);

                    compiledView.Template(writer, model, renderContext, viewLocationResult);
                    writer.Flush();
                }
            };
        }

        private CompiledView GetOrCompileTemplate(ViewLocationResult viewLocationResult, IRenderContext renderContext)
        {
            var view = renderContext.ViewCache.GetOrAdd(
                viewLocationResult,
                x =>
                {
                    using (var reader = x.Contents.Invoke())
                    {
                        var compiledView = new CompiledView(reader);
                        return compiledView;
                    }
                });

            using (var textReader = viewLocationResult.Contents.Invoke())
                view.RecompileIfNewer(textReader);

            return view;
        }
    }
}