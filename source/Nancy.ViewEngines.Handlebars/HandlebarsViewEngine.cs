using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;
using Nancy.Responses;
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
                    var template = this.GetOrCompileTemplate(viewLocationResult, renderContext);

                    var writer = new StreamWriter(stream);

                    template(writer, model, renderContext, viewLocationResult);
                    writer.Flush();
                }
            };
        }

        private Action<TextWriter, object, IRenderContext, ViewLocationResult> GetOrCompileTemplate(ViewLocationResult viewLocationResult, IRenderContext renderContext)
        {
            var viewFactory = renderContext.ViewCache.GetOrAdd(
                viewLocationResult,
                x =>
                {
                    using (var reader = x.Contents.Invoke())
                        return this.GetCompiledTemplate<dynamic>(reader);
                });

            var view = viewFactory.Invoke();

            return view;
        }

        private Func<Action<TextWriter, object, IRenderContext, ViewLocationResult>> GetCompiledTemplate<TModel>(TextReader reader)
        {
            var template = HandlebarsDotNet.Handlebars.Compile(reader);

            Func<Action<TextWriter, object>> nakedTemplate = () => template;

            return CreateTemplateWithLayout(nakedTemplate);
        }

        private Func<Action<TextWriter, object, IRenderContext, ViewLocationResult>> CreateTemplateWithLayout(Func<Action<TextWriter, object>> nakedTemplate)
        {
            return () =>
            {
                return ((writer, model, context, viewLocationResult) =>
                {
                    WriteView(nakedTemplate, writer, model, context, viewLocationResult);
                });
            };
        }

        private static void WriteView(Func<Action<TextWriter, object>> nakedTemplate,
            TextWriter writer, object model, IRenderContext context, ViewLocationResult viewLocationResult)
        {
            var layoutLocationResult = new DefaultLayoutResolver().ResolveLayoutLocatioon(viewLocationResult, context);
            if (layoutLocationResult != null)
                WriteWithLayout(nakedTemplate, writer, model, layoutLocationResult, context);
            else
                WriteNakedTemplate(nakedTemplate, writer, model);
        }

        private static void WriteWithLayout(Func<Action<TextWriter, object>> nakedTemplate, TextWriter writer, object model, ViewLocationResult layoutLocationResult, IRenderContext context)
        {
            using (var bodyStream = new MemoryStream())
            {
                using (var bodyWriter = new StreamWriter(bodyStream, writer.Encoding))
                {
                    WriteNakedTemplate(nakedTemplate, bodyWriter, model);
                }
                var layoutViewFactory = context.ViewCache.GetOrAdd<Func<Action<TextWriter, object>>>(
                    layoutLocationResult,
                    x =>
                    {
                        using (var reader = x.Contents.Invoke())
                        {
                            var layoutTemplate = HandlebarsDotNet.Handlebars.Compile(reader);
                            return () => layoutTemplate;
                        }
                    });

                var layout = layoutViewFactory.Invoke();
                layout(writer, new { body = writer.Encoding.GetString(bodyStream.ToArray()) });
            }
        }

        private static void WriteNakedTemplate(Func<Action<TextWriter, object>> nakedTemplate, TextWriter writer, object model)
        {
            nakedTemplate?.Invoke()(writer, model);
        }

        private Action<TextWriter, object> GetPartial(IRenderContext renderContext, string name, dynamic model)
        {
            var view = renderContext.LocateView(name, model);
            return this.GetOrCompileTemplate(view, renderContext);
        }
    }
}