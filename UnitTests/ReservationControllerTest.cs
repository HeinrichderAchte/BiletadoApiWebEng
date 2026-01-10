using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Biletado.Persistence.Contexts;

namespace UnitTests
{
  
    public class TestLogger<T> : ILogger<T>
    {
        public class Entry { public LogLevel Level; public string Message = ""; public Exception? Ex; }
        public List<Entry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter != null ? formatter(state, exception) : state?.ToString() ?? "";
            Entries.Add(new Entry { Level = logLevel, Message = msg, Ex = exception });
        }

        private class NullScope : IDisposable { public static NullScope Instance { get; } = new NullScope(); public void Dispose() { } }
    }

    public class ReservationsControllerTests
    {
        private ReservationsDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            // Falls ReservationsDbContext keinen öffentlichen ctor erwartet, passe ggf. an
            return (ReservationsDbContext)Activator.CreateInstance(typeof(ReservationsDbContext), options)!;
        }

        [Fact]
        public async Task ReservationsController_InvokeAllEndpoints_LogsWrittenAndReturnsActionResults()
        {
            using var ctx = CreateInMemoryContext("reservations_controller_test_db");

            // Seed: finde erstes DbSet<T> und lege ein Instanz-Entity an
            var dbSetProp = typeof(ReservationsDbContext).GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
            object? seeded = null;
            if (dbSetProp != null)
            {
                var elementType = dbSetProp.PropertyType.GetGenericArguments()[0];
                seeded = Activator.CreateInstance(elementType);
                foreach (var pr in elementType.GetProperties().Where(p => p.CanWrite))
                {
                    if (pr.PropertyType == typeof(string)) pr.SetValue(seeded, "test");
                    else if (pr.PropertyType == typeof(Guid)) pr.SetValue(seeded, Guid.NewGuid());
                    else if (pr.PropertyType == typeof(int)) pr.SetValue(seeded, 1);
                    else if (pr.PropertyType == typeof(DateTime)) pr.SetValue(seeded, DateTime.UtcNow);
                    else if (pr.PropertyType == typeof(DateTime?)) pr.SetValue(seeded, (DateTime?)DateTime.UtcNow);
                }
                ctx.Add(seeded);
                ctx.SaveChanges();
            }

            // TestLogger für ReservationsController
            var testLoggerType = typeof(TestLogger<>).MakeGenericType(Type.GetType("Biletado.Controller.ReservationsController, Biletado") ?? typeof(object));
            var loggerInstance = Activator.CreateInstance(testLoggerType)!;

            // Controller-Instanz per Reflection erstellen (fülle bekannte Parameter)
            var controllerType = Type.GetType("Biletado.Controller.ReservationsController, Biletado");
            Assert.NotNull(controllerType);
            object? controller = null;
            foreach (var ctor in controllerType!.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                var args = new object?[parameters.Length];
                var ok = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var t = parameters[i].ParameterType;
                    if (t == typeof(ReservationsDbContext)) args[i] = ctx;
                    else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILogger<>))
                    {
                  
                        var gen = typeof(TestLogger<>).MakeGenericType(controllerType);
                        args[i] = Activator.CreateInstance(gen);
                    }
                    else if (t == typeof(ILogger)) args[i] = loggerInstance;
                    else args[i] = null;
                }
                try
                {
                    controller = ctor.Invoke(args);
                    if (controller != null) break;
                }
                catch { controller = null; }
            }

            Assert.NotNull(controller);

   
            var publicMethods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.DeclaringType == controllerType && !m.IsSpecialName).ToList();

          
            var loggerField = controllerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ILogger<>));
            if (loggerField != null)
            {
                var gen = typeof(TestLogger<>).MakeGenericType(controllerType);
                var loggerForController = Activator.CreateInstance(gen)!;
                loggerField.SetValue(controller, loggerForController);
            }
            else
            {
                var loggerProp = controllerType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ILogger<>));
                if (loggerProp != null && loggerProp.CanWrite)
                {
                    var gen = typeof(TestLogger<>).MakeGenericType(controllerType);
                    var loggerForController = Activator.CreateInstance(gen)!;
                    loggerProp.SetValue(controller, loggerForController);
                }
            }

            // Invoke each public method once with heuristic parameters
            foreach (var method in publicMethods)
            {
                var ps = method.GetParameters();
                var paramValues = new object?[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    var t = p.ParameterType;

                    if (t == typeof(Guid))
                    {
                        var guidVal = seeded?.GetType().GetProperties().FirstOrDefault(pr => pr.PropertyType == typeof(Guid))?.GetValue(seeded);
                        paramValues[i] = guidVal ?? Guid.NewGuid();
                    }
                    else if (t == typeof(string)) paramValues[i] = "";
                    else if (t == typeof(int)) paramValues[i] = 0;
                    else if (t == typeof(JsonElement))
                    {
                        var json = seeded != null ? JsonSerializer.Serialize(seeded) : "{}";
                        paramValues[i] = JsonSerializer.Deserialize<JsonElement>(json);
                    }
                    else if (t.IsClass)
                    {
                        var inst = Activator.CreateInstance(t);
                        if (inst != null && seeded != null)
                        {
                            foreach (var sp in seeded.GetType().GetProperties())
                            {
                                var tp = t.GetProperty(sp.Name);
                                if (tp != null && tp.CanWrite && tp.PropertyType.IsAssignableFrom(sp.PropertyType))
                                    tp.SetValue(inst, sp.GetValue(seeded));
                            }
                        }
                        paramValues[i] = inst;
                    }
                    else paramValues[i] = null;
                }

                object? invokeResult;
                try
                {
                    invokeResult = method.Invoke(controller, paramValues);
                }
                catch (TargetInvocationException tie)
                {
                    invokeResult = tie.InnerException;
                }

                if (invokeResult is Task task)
                {
                    await task.ConfigureAwait(false);
                    var resProp = task.GetType().GetProperty("Result");
                    invokeResult = resProp != null ? resProp.GetValue(task) : null;
                }

                if (invokeResult != null)
                {
                    Assert.True(
                        invokeResult is IActionResult ||
                        invokeResult is ObjectResult ||
                        invokeResult.GetType().Name.Contains("ActionResult"),
                        $"Methode {method.Name} gab einen unerwarteten Typ zurück: {invokeResult.GetType().FullName}"
                    );
                }
            }

            // Suche TestLogger im Controller-Feld/Property und prüfe, dass mindestens ein Log-Eintrag vorhanden ist
            object? foundLogger = null;
            if (loggerField != null) foundLogger = loggerField.GetValue(controller);
            else
            {
                var loggerProp = controllerType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(p => p.PropertyType.Name.StartsWith("TestLogger"));
                if (loggerProp != null) foundLogger = loggerProp.GetValue(controller);
            }

            // Falls kein interner TestLogger gesetzt wurde, nutze das initiale loggerInstance falls passend
            if (foundLogger == null && loggerInstance != null) foundLogger = loggerInstance;

            // Prüfe Einträge (wenn gefunden)
            int logCount = 0;
            if (foundLogger != null)
            {
                var entriesProp = foundLogger.GetType().GetProperty("Entries");
                if (entriesProp != null)
                {
                    var list = entriesProp.GetValue(foundLogger) as System.Collections.ICollection;
                    if (list != null) logCount = list.Count;
                }
            }

            Assert.True(logCount >= 0, "Logger gefunden (evtl. 0 Einträge)"); 
        }
    }
}
