using System.Reflection;
using System.Runtime.InteropServices;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Versões/ambiente técnico usados por "Copiar informações de suporte" e pelo cabeçalho de
    /// cada entrada de log (ver docs/listforge-classic-parity.md, grupo "Configurações
    /// aplicáveis ao integrado", e docs/ARCHITECTURE.md). Centralizado aqui para não duplicar a
    /// leitura por reflexão em vários lugares (<see cref="PxLogger"/>, área de Diagnóstico,
    /// <see cref="SupportPackageService"/>).
    /// </summary>
    public static class PxVersionInfo
    {
        /// <summary>
        /// Versão real do PX Tool Box DEV, embutida no assembly em tempo de build por
        /// build-release.ps1 (PXToolBoxWriteVersionInfo, ver PXToolBox.CorelHost.Dev.csproj) —
        /// mesmo mecanismo já usado por ControlUI.ResolveAppVersion. Numa build local fora do
        /// script de release, "0.0.0-dev.local" deixa isso óbvio em vez de mostrar um número que
        /// não corresponde ao que foi de fato compilado.
        /// </summary>
        public static string PxToolBoxVersion =>
            typeof(PxVersionInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? "0.0.0-dev.local";

        /// <summary>Versão do assembly ListForge.Core realmente referenciado por esta build (não
        /// cravada — lida do assembly carregado, para nunca divergir do que foi de fato
        /// compilado/publicado).</summary>
        public static string ListForgeCoreVersion =>
            typeof(ListForge.Core.SizeHelper).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        /// <summary>Runtime/arquitetura do processo do CorelDRAW hospedando o add-on (não da
        /// máquina em geral) — útil para diferenciar x86/x64 e a versão do .NET Framework em uso
        /// quando o add-on não se comporta como esperado num ambiente específico.</summary>
        public static string RuntimeDescription =>
            $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.OSArchitecture}, {RuntimeInformation.OSDescription})";
    }
}
