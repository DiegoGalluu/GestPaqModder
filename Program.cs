using System;
using System.Windows.Forms;

namespace GestPaqModder
{
    // Punto de arranque de la aplicacion WinForms.
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicacion.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // Registramos manejadores globales para capturar errores no controlados.
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Inicializacion visual estandar de WinForms.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Lanzamos el formulario principal.
            Application.Run(new FormularioPrincipal());
        }

        // Error no controlado en el hilo de UI.
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MostrarErrorNoControlado(e.Exception);
        }

        // Error no controlado en otros hilos/dominio.
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var excepcion = e.ExceptionObject as Exception;
            MostrarErrorNoControlado(excepcion);
        }

        // Mensaje amigable de error para el usuario final.
        private static void MostrarErrorNoControlado(Exception excepcion)
        {
            var detalle = excepcion == null ? "Error desconocido." : excepcion.Message;
            MessageBox.Show(
                "Se produjo un error no controlado:\n" + detalle,
                "SegMode - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
