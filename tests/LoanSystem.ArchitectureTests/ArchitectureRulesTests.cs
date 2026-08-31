using System.Reflection;
using Mono.Cecil;

namespace LoanSystem.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    private static readonly Assembly[] Modules =
    [
        typeof(Modules.IdentityAccess.ModuleMarker).Assembly,
        typeof(Modules.Borrowers.ModuleMarker).Assembly,
        typeof(Modules.LoanProducts.ModuleMarker).Assembly,
        typeof(Modules.LoanOrigination.ModuleMarker).Assembly,
        typeof(Modules.LoanAccounts.ModuleMarker).Assembly,
        typeof(Modules.Disbursements.ModuleMarker).Assembly,
        typeof(Modules.Treasury.ModuleMarker).Assembly,
        typeof(Modules.Repayments.ModuleMarker).Assembly,
        typeof(Modules.Documents.ModuleMarker).Assembly,
        typeof(Modules.Audit.ModuleMarker).Assembly,
        typeof(Modules.Reporting.ModuleMarker).Assembly,
    ];

    [Fact]
    public void Every_planned_module_and_layer_is_inspected()
    {
        Assert.Equal(11, Modules.Length);
        Assert.All(Modules, module =>
        {
            var namespaces = ReadModule(module).MainModule.Types.Select(type => type.Namespace).ToArray();
            Assert.Contains(namespaces, value => value.EndsWith(".Domain", StringComparison.Ordinal));
            Assert.Contains(namespaces, value => value.EndsWith(".Application", StringComparison.Ordinal));
            Assert.Contains(namespaces, value => value.EndsWith(".Infrastructure", StringComparison.Ordinal));
            Assert.Contains(namespaces, value => value.EndsWith(".Presentation", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Module_implementations_do_not_reference_other_module_implementations()
    {
        var moduleNames = Modules.Select(module => module.GetName().Name!).ToHashSet(StringComparer.Ordinal);

        Assert.All(Modules, module => Assert.Empty(
            module.GetReferencedAssemblies().Where(reference =>
                reference.Name is not null &&
                moduleNames.Contains(reference.Name))));
    }

    [Fact]
    public void Domain_and_application_layers_obey_clean_architecture_dependencies()
    {
        Assert.All(Modules, module =>
        {
            var definitions = ReadModule(module).MainModule.Types.SelectMany(Flatten).ToArray();
            AssertLayerDoesNotReference(definitions, ".Domain", [".Application", ".Infrastructure", ".Presentation"]);
            AssertLayerDoesNotReference(definitions, ".Application", [".Infrastructure", ".Presentation"]);
            AssertLayerDoesNotReference(definitions, ".Presentation", [".Infrastructure"]);
        });
    }

    [Fact]
    public void Borrowers_application_does_not_reference_database_providers()
    {
        var definitions = ReadModule(typeof(Modules.Borrowers.ModuleMarker).Assembly)
            .MainModule.Types.SelectMany(Flatten)
            .Where(type => type.Namespace.EndsWith(".Application", StringComparison.Ordinal));

        Assert.All(definitions, type => Assert.DoesNotContain(
            ReferencedTypeNames(type),
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || reference.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)));
    }

    [Fact]
    public void Loan_products_domain_and_application_do_not_reference_providers_or_aspnet()
    {
        var definitions = ReadModule(typeof(Modules.LoanProducts.ModuleMarker).Assembly).MainModule.Types.SelectMany(Flatten).ToArray();
        var protectedTypes = definitions.Where(type => type.Namespace.EndsWith(".Domain", StringComparison.Ordinal)
            || type.Namespace.EndsWith(".Application", StringComparison.Ordinal));

        Assert.All(protectedTypes, type => Assert.DoesNotContain(
            ReferencedTypeNames(type),
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || reference.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
                || reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)));
    }

    [Fact]
    public void Loan_products_public_integration_contract_is_provider_neutral()
    {
        var contract = typeof(Contracts.ILoanProductsModule);
        Assert.Equal(typeof(Contracts.IModuleContract), contract.GetInterfaces().Single());
        Assert.All(contract.GetMethods(), method =>
        {
            Assert.DoesNotContain("Infrastructure", method.ReturnType.FullName, StringComparison.Ordinal);
            Assert.DoesNotContain("DbContext", method.ReturnType.FullName, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Transactional_modules_do_not_depend_on_reporting()
    {
        var reportingName = typeof(Modules.Reporting.ModuleMarker).Assembly.GetName().Name;
        var transactionalModules = Modules.Where(module => module != typeof(Modules.Reporting.ModuleMarker).Assembly);

        Assert.All(transactionalModules, module =>
            Assert.DoesNotContain(module.GetReferencedAssemblies(), reference => reference.Name == reportingName));
    }

    [Fact]
    public void Cross_module_contracts_use_only_the_contracts_assembly()
    {
        var allowedContract = typeof(Contracts.IModuleContract).Assembly.GetName().Name;
        var modulePrefix = "LoanSystem.Modules.";

        Assert.All(Modules, module => Assert.All(module.GetReferencedAssemblies(), reference =>
            Assert.True(!reference.Name!.StartsWith(modulePrefix, StringComparison.Ordinal) || reference.Name == allowedContract,
                $"{module.GetName().Name} bypasses LoanSystem.Contracts through {reference.Name}.")));
    }

    private static AssemblyDefinition ReadModule(Assembly module) => AssemblyDefinition.ReadAssembly(module.Location);

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type) =>
        new[] { type }.Concat(type.NestedTypes.SelectMany(Flatten));

    private static IEnumerable<string> ReferencedTypeNames(TypeDefinition type)
    {
        if (type.BaseType is not null) yield return type.BaseType.FullName;
        foreach (var contract in type.Interfaces) yield return contract.InterfaceType.FullName;
        foreach (var field in type.Fields) yield return field.FieldType.FullName;
        foreach (var property in type.Properties) yield return property.PropertyType.FullName;
        foreach (var method in type.Methods)
        {
            yield return method.ReturnType.FullName;
            foreach (var parameter in method.Parameters) yield return parameter.ParameterType.FullName;
            if (!method.HasBody) continue;
            foreach (var variable in method.Body.Variables) yield return variable.VariableType.FullName;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is TypeReference typeReference) yield return typeReference.FullName;
                if (instruction.Operand is MethodReference methodReference) yield return methodReference.DeclaringType.FullName;
                if (instruction.Operand is FieldReference fieldReference) yield return fieldReference.DeclaringType.FullName;
            }
        }
    }

    private static void AssertLayerDoesNotReference(
        IEnumerable<TypeDefinition> definitions,
        string sourceLayer,
        IReadOnlyCollection<string> forbiddenLayers)
    {
        var sourceTypes = definitions.Where(type => type.Namespace.EndsWith(sourceLayer, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(sourceTypes);

        foreach (var type in sourceTypes)
        {
            var references = ReferencedTypeNames(type).ToArray();

            Assert.DoesNotContain(references, reference =>
                forbiddenLayers.Any(layer => reference.Contains(layer, StringComparison.Ordinal)));
        }
    }
}
