using System;
using System.Runtime.InteropServices;
using Corel.Interop.VGCore;

namespace PXToolBoxDev.Corel
{
    /// <summary>
    /// Obtém a instância do CorelDRAW em execução que hospeda este add-on.
    ///
    /// PONTO EM ABERTO (validar no smoke test real): a documentação do mecanismo de add-on
    /// "wpfhost" usado pelo CorelDRAW 2026 (ver docs/ARCHITECTURE.md — descoberto via o add-on
    /// SISBolt já instalado, não documentado publicamente) não especifica como a instância de
    /// Application chega até o controle hospedado. Duas hipóteses foram implementadas em ordem:
    /// (1) se o host chamar um construtor que recebe o Application, ControlUI(object) é usado;
    /// (2) caso contrário, como fallback, usa-se Marshal.GetActiveObject com o ProgID da versão
    /// 27 para localizar a instância em execução via Running Object Table.
    /// </summary>
    internal static class CorelApplicationLocator
    {
        private const string ProgId27 = "CorelDRAW.Application.27";

        public static Application? TryGetRunningApplication()
        {
            try
            {
                var obj = Marshal.GetActiveObject(ProgId27);
                return obj as Application;
            }
            catch (COMException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
