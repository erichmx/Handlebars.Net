using HandlebarsDotNet;
using Nancy.Extensions;
using Nancy.TinyIoc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nancy.ViewEngines.Handlebars
{
    internal class CompiledView
    {
        public Action<TextWriter, object, IRenderContext, ViewLocationResult> Template { get; set; }
        public DateTime ModifiedOn { get; set; } = DateTime.MaxValue;
        public bool IsFromFile { get; set; } = false;

        public static HandlebarsConfiguration handlebarsConfiguration = new HandlebarsConfiguration();

        // Lazy-load Handlebars environment to ensure thread safety.  See Jon Skeet's excellent article on this for more info. http://csharpindepth.com/Articles/General/Singleton.aspx
        private static readonly Lazy<IHandlebars> lazy = new Lazy<IHandlebars>(() => HandlebarsDotNet.Handlebars.Create(handlebarsConfiguration));

        private static IHandlebars HandlebarsProcessor { get { return lazy.Value; } }

        private static ILayoutResolver _defaultLayoutResolver = new DefaultLayoutResolver();

        private ILayoutResolver LayoutResolver
        {
            get
            {
                ILayoutResolver res;
                TinyIoCContainer.Current.TryResolve<ILayoutResolver>(out res);
                return res ?? _defaultLayoutResolver;
            }
        }

        static CompiledView()
        {
            HandlebarsProcessor.RegisterHelper("Path", (output, context, arguments) =>
            {
                if (arguments.Length >= 1)
                {
                    var nancyContext = (NancyContext)context.NancyContext;
                    output.Write(nancyContext.ToFullPath(arguments[0].ToString()));
                }
            });
        }

        public CompiledView(TextReader reader)
        {
            Compile(reader);
        }

        private void Compile(TextReader reader)
        {
            Template = GetCompiledTemplate(reader);
            var streamReader = reader as StreamReader;
            var fileStream = streamReader?.BaseStream as FileStream;
            System.Diagnostics.Debug.WriteLine(($"From file: {fileStream != null}"));
            if (fileStream != null)
            {
                IsFromFile = true;
                var file = new FileInfo(fileStream.Name);
                System.Diagnostics.Debug.WriteLine(($"Template path: {fileStream.Name}"));
                ModifiedOn = file.LastWriteTimeUtc;
            }
        }

        public void RecompileIfNewer(TextReader reader)
        {
            if (!IsFromFile)
                return;

            var streamReader = reader as StreamReader;
            var fileStream = streamReader?.BaseStream as FileStream;
            System.Diagnostics.Debug.WriteLine(($"From file: {fileStream != null}"));
            if (fileStream != null)
            {
                var file = new FileInfo(fileStream.Name);
                System.Diagnostics.Debug.WriteLine(($"Template path: {fileStream.Name}"));
                if (ModifiedOn < file.LastWriteTimeUtc)
                    Compile(reader);
            }
        }

        private Action<TextWriter, dynamic, IRenderContext, ViewLocationResult> GetCompiledTemplate(TextReader reader)
        {
            var nakedTemplate = HandlebarsProcessor.Compile(reader);

            return CreateTemplateWithLayout(nakedTemplate);
        }

        private Action<TextWriter, dynamic, IRenderContext, ViewLocationResult> CreateTemplateWithLayout(Action<TextWriter, dynamic> nakedTemplate)
        {
            return ((writer, model, context, viewLocationResult) =>
            {
                WriteView(nakedTemplate, writer, model, context, viewLocationResult);
            });
        }

        private void WriteView(Action<TextWriter, dynamic> nakedTemplate,
            TextWriter writer, dynamic model, IRenderContext context, ViewLocationResult viewLocationResult)
        {
            model.NancyContext = context.Context;
            var layoutLocationResult = LayoutResolver.ResolveLayoutLocatioon(viewLocationResult, context);
            if (layoutLocationResult != null)
                WriteWithLayout(nakedTemplate, writer, model, layoutLocationResult, context);
            else
                WriteNakedTemplate(nakedTemplate, writer, model);
        }

        private static void WriteWithLayout(Action<TextWriter, dynamic> nakedTemplate, TextWriter writer, dynamic model, ViewLocationResult layoutLocationResult, IRenderContext context)
        {
            using (var bodyStream = new MemoryStream())
            {
                using (var bodyWriter = new StreamWriter(bodyStream, writer.Encoding))
                {
                    WriteNakedTemplate(nakedTemplate, bodyWriter, model);
                }
                var layoutViewFactory = context.ViewCache.GetOrAdd<Func<Action<TextWriter, dynamic>>>(
                    layoutLocationResult,
                    x =>
                    {
                        using (var reader = x.Contents.Invoke())
                        {
                            var layoutTemplate = HandlebarsProcessor.Compile(reader);
                            return () => layoutTemplate;
                        }
                    });

                var layout = layoutViewFactory.Invoke();
                layout(writer, new { body = writer.Encoding.GetString(bodyStream.ToArray()), NancyContext = model.NancyContext });
            }
        }

        private static void WriteNakedTemplate(Action<TextWriter, dynamic> nakedTemplate, TextWriter writer, dynamic model)
        {
            nakedTemplate?.Invoke(writer, model);
        }
    }
}