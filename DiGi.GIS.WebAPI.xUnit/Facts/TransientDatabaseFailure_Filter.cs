using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using System;
using System.Linq;
using System.Reflection;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the decision of <see cref="TransientDatabaseFailureAttribute"/> (DiGi-Organization/DiGi.GIS.WebAPI#71): a transient <see cref="NpgsqlException"/> - directly or as an inner exception - becomes 503 with <c>Retry-After: 30</c>; a non-transient one and any other exception are left unhandled, so the host keeps answering 500.
        /// <para>The transient exception is the one Npgsql itself classifies: an <see cref="NpgsqlException"/> over a <see cref="TimeoutException"/> is transient, a bare one is not.</para>
        /// </summary>
        [Fact]
        public void TransientDatabaseFailure_Filter_HandlesTransientOnly()
        {
            TransientDatabaseFailureAttribute transientDatabaseFailureAttribute = new();

            NpgsqlException npgsqlException_Transient = new("read", new TimeoutException());
            Assert.True(npgsqlException_Transient.IsTransient);

            foreach (Exception exception in new Exception[] { npgsqlException_Transient, new InvalidOperationException("wrapped", npgsqlException_Transient) })
            {
                ExceptionContext exceptionContext = ExceptionContext(exception);
                transientDatabaseFailureAttribute.OnException(exceptionContext);

                Assert.True(exceptionContext.ExceptionHandled);
                ObjectResult objectResult = Assert.IsType<ObjectResult>(exceptionContext.Result);
                Assert.Equal(503, objectResult.StatusCode);
                Assert.Equal(TransientDatabaseFailureAttribute.Message, objectResult.Value);
                Assert.Equal("30", (string?)exceptionContext.HttpContext.Response.Headers["Retry-After"]);
            }

            foreach (Exception exception in new Exception[] { new NpgsqlException("syntax"), new InvalidOperationException("not a database failure") })
            {
                Assert.False(exception is NpgsqlException { IsTransient: true });

                ExceptionContext exceptionContext = ExceptionContext(exception);
                transientDatabaseFailureAttribute.OnException(exceptionContext);

                Assert.False(exceptionContext.ExceptionHandled);
                Assert.Null(exceptionContext.Result);
                Assert.False(exceptionContext.HttpContext.Response.Headers.ContainsKey("Retry-After"));
            }
        }

        /// <summary>
        /// Every controller of <c>DiGi.GIS.WebAPI</c> carries <see cref="TransientDatabaseFailureAttribute"/>, so an action without its own transient catch - including one added later, in a controller added later - still answers 503 rather than 500 (DiGi-Organization/DiGi.GIS.WebAPI#71).
        /// </summary>
        [Fact]
        public void TransientDatabaseFailure_EveryControllerHasFilter()
        {
            Type[] types = typeof(OrtoDatasController).Assembly.GetTypes().Where(x => !x.IsAbstract && typeof(WebAPIController).IsAssignableFrom(x)).ToArray();
            Assert.NotEmpty(types);

            string[] missing = types.Where(x => x.GetCustomAttribute<TransientDatabaseFailureAttribute>() is null).Select(x => x.Name).ToArray();
            Assert.True(missing.Length == 0, $"Controllers without [TransientDatabaseFailure]: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// Builds an <see cref="ExceptionContext"/> for <paramref name="exception"/> over a fresh <see cref="DefaultHttpContext"/>.
        /// </summary>
        private static ExceptionContext ExceptionContext(Exception exception)
        {
            ActionContext actionContext = new(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            return new ExceptionContext(actionContext, []) { Exception = exception };
        }
    }
}
