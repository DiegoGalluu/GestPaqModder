using GestPaqModder.Modelos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GestPaqModder.Servicios
{
    // Servicio que carga, analiza y modifica ControlProceso.ini.
    public class ServicioIni
    {
        // Regex para detectar cabeceras de seccion [NOMBRE].
        private static readonly Regex RegexLineaSeccion = new Regex(@"^\s*\[(?<nombre>[^\]]+)\]\s*$", RegexOptions.Compiled);

        // Regex para detectar la clave Enable preservando espacios/comentarios.
        private static readonly Regex RegexClaveEnable = new Regex(@"^(?<inicio>\s*Enable\s*=\s*)(?<valor>[^;#]*?)(?<fin>\s*(?:[;#].*)?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Copia editable del archivo por lineas para cambios posicionales.
        private readonly List<string> _lineas = new List<string>();

        // Se mantiene para guardar con la misma codificacion de origen.
        private Encoding _codificacionArchivo = Encoding.Default;

        // Se mantiene para guardar con el mismo tipo de salto de linea de origen.
        private string _saltoLinea = Environment.NewLine;

        public string RutaIniActual { get; private set; }

        public string ContenidoOriginalSesion { get; private set; }

        // Secciones de interes detectadas (BASCULA/VOLUMETRICO).
        public IReadOnlyList<SeccionControl> SeccionesObjetivo { get; private set; } = new List<SeccionControl>();

        // Ruta por defecto usada por el formulario para autoload.
        public string ObtenerRutaPredeterminada()
        {
            return Path.Combine(Application.StartupPath, "ControlProceso.ini");
        }

        // Limpia una ruta potencialmente pegada con comillas y espacios.
        public string LimpiarRutaManual(string rutaEntrada)
        {
            if (rutaEntrada == null)
            {
                return string.Empty;
            }

            var ruta = rutaEntrada.Trim();

            while (ruta.Length >= 2)
            {
                var tieneComillasDobles = ruta.StartsWith("\"") && ruta.EndsWith("\"");
                var tieneComillasSimples = ruta.StartsWith("'") && ruta.EndsWith("'");

                if (!tieneComillasDobles && !tieneComillasSimples)
                {
                    break;
                }

                ruta = ruta.Substring(1, ruta.Length - 2).Trim();
            }

            return ruta;
        }

        // Carga un INI en memoria y extrae las secciones objetivo.
        public bool IntentarCargarIni(string rutaEntrada, out string mensajeError, out List<string> advertencias)
        {
            advertencias = new List<string>();
            mensajeError = null;

            var rutaLimpia = LimpiarRutaManual(rutaEntrada);
            if (string.IsNullOrWhiteSpace(rutaLimpia))
            {
                mensajeError = "Debe indicar una ruta valida a un archivo INI.";
                return false;
            }

            if (!File.Exists(rutaLimpia))
            {
                mensajeError = "El archivo indicado no existe:\n" + rutaLimpia;
                return false;
            }

            try
            {
                string contenido;
                using (var lector = new StreamReader(rutaLimpia, true))
                {
                    contenido = lector.ReadToEnd();
                    _codificacionArchivo = lector.CurrentEncoding ?? Encoding.Default;
                }

                if (string.IsNullOrWhiteSpace(contenido))
                {
                    mensajeError = "El archivo INI esta vacio.";
                    return false;
                }

                _saltoLinea = DetectarSaltoLinea(contenido);

                // Volcamos contenido a lista editable por indice.
                _lineas.Clear();
                _lineas.AddRange(SepararLineas(contenido));

                if (_lineas.Count == 0)
                {
                    mensajeError = "No se pudo leer contenido valido del archivo INI.";
                    return false;
                }

                RutaIniActual = Path.GetFullPath(rutaLimpia);
                ContenidoOriginalSesion = contenido;

                ReanalizarSecciones(out advertencias);
                return true;
            }
            catch (IOException ex)
            {
                mensajeError = "No se pudo leer el archivo porque esta bloqueado o en uso por otro proceso.\n" + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                mensajeError = "No tiene permisos para leer el archivo.\n" + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                mensajeError = "Error inesperado al cargar el archivo INI.\n" + ex.Message;
                return false;
            }
        }

        // Reconstruye la lista de secciones objetivo en base al contenido actual en memoria.
        public void ReanalizarSecciones(out List<string> advertencias)
        {
            advertencias = new List<string>();
            var secciones = new List<SeccionControl>();

            for (var i = 0; i < _lineas.Count; i++)
            {
                var coincidenciaSeccion = RegexLineaSeccion.Match(_lineas[i]);
                if (!coincidenciaSeccion.Success)
                {
                    continue;
                }

                var nombreSeccion = coincidenciaSeccion.Groups["nombre"].Value.Trim();
                var tipo = DeterminarTipoSeccion(nombreSeccion);
                if (tipo == TipoSeccionControl.Desconocido)
                {
                    continue;
                }

                var seccion = new SeccionControl
                {
                    NombreSeccion = nombreSeccion,
                    Tipo = tipo,
                    IndiceLineaSeccion = i,
                    IndiceLineaEnable = -1,
                    Enable = false,
                    TieneEnable = false
                };

                // Recorremos solo hasta la siguiente seccion para localizar Enable.
                var limiteSuperior = ObtenerIndiceInicioSiguienteSeccion(i + 1);
                for (var j = i + 1; j < limiteSuperior; j++)
                {
                    var coincidenciaEnable = RegexClaveEnable.Match(_lineas[j]);
                    if (!coincidenciaEnable.Success)
                    {
                        continue;
                    }

                    var valorTexto = coincidenciaEnable.Groups["valor"].Value.Trim();
                    seccion.IndiceLineaEnable = j;
                    seccion.TieneEnable = true;

                    if (valorTexto == "1")
                    {
                        seccion.Enable = true;
                    }
                    else if (valorTexto == "0")
                    {
                        seccion.Enable = false;
                    }
                    else
                    {
                        seccion.Enable = false;
                        advertencias.Add("La seccion [" + seccion.NombreSeccion + "] tiene un valor Enable no valido (" + valorTexto + "). Se tratara como 0.");
                    }

                    break;
                }

                if (!seccion.TieneEnable)
                {
                    advertencias.Add("La seccion [" + seccion.NombreSeccion + "] no contiene la clave Enable.");
                }

                secciones.Add(seccion);
            }

            SeccionesObjetivo = secciones;
        }

        // Cambia Enable para una seccion y persiste en disco.
        public bool IntentarCambiarEnable(SeccionControl seccion, bool nuevoEstado, out string mensajeError)
        {
            mensajeError = null;

            if (seccion == null)
            {
                mensajeError = "No se ha seleccionado una seccion valida.";
                return false;
            }

            if (!seccion.TieneEnable || seccion.IndiceLineaEnable < 0 || seccion.IndiceLineaEnable >= _lineas.Count)
            {
                mensajeError = "La seccion seleccionada no contiene una clave Enable modificable.";
                return false;
            }

            var indice = seccion.IndiceLineaEnable;
            var lineaOriginal = _lineas[indice];
            var reemplazo = RegexClaveEnable.Replace(
                lineaOriginal,
                m => m.Groups["inicio"].Value + (nuevoEstado ? "1" : "0") + m.Groups["fin"].Value,
                1);

            // Si no hay cambios por alguna razon, forzamos un formato consistente.
            if (reemplazo == lineaOriginal)
            {
                reemplazo = Regex.Replace(
                    lineaOriginal,
                    @"^\s*Enable\s*=\s*.*$",
                    "Enable = " + (nuevoEstado ? "1" : "0"),
                    RegexOptions.IgnoreCase);
            }

            _lineas[indice] = reemplazo;

            if (!IntentarGuardarArchivo(out mensajeError))
            {
                // Revertimos memoria si no se pudo guardar en disco.
                _lineas[indice] = lineaOriginal;
                return false;
            }

            seccion.Enable = nuevoEstado;
            return true;
        }

        // Restaura exactamente el contenido original de esta sesion.
        public bool IntentarRestaurarContenidoOriginal(out string mensajeError, out List<string> advertencias)
        {
            advertencias = new List<string>();
            mensajeError = null;

            if (string.IsNullOrWhiteSpace(RutaIniActual) || string.IsNullOrEmpty(ContenidoOriginalSesion))
            {
                mensajeError = "No hay un archivo INI cargado para restaurar.";
                return false;
            }

            try
            {
                File.WriteAllText(RutaIniActual, ContenidoOriginalSesion, _codificacionArchivo);
                _lineas.Clear();
                _lineas.AddRange(SepararLineas(ContenidoOriginalSesion));
                ReanalizarSecciones(out advertencias);
                return true;
            }
            catch (IOException ex)
            {
                mensajeError = "No se pudo restaurar el archivo porque esta bloqueado o en uso.\n" + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                mensajeError = "No tiene permisos para restaurar el archivo.\n" + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                mensajeError = "Error inesperado al restaurar el archivo INI.\n" + ex.Message;
                return false;
            }
        }

        // Guarda la representacion en memoria en el archivo INI actual.
        private bool IntentarGuardarArchivo(out string mensajeError)
        {
            mensajeError = null;

            if (string.IsNullOrWhiteSpace(RutaIniActual))
            {
                mensajeError = "No hay una ruta de archivo INI cargada.";
                return false;
            }

            try
            {
                var contenido = string.Join(_saltoLinea, _lineas);
                File.WriteAllText(RutaIniActual, contenido, _codificacionArchivo);
                return true;
            }
            catch (IOException ex)
            {
                mensajeError = "No se pudo guardar el archivo INI porque esta bloqueado o en uso.\n" + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                mensajeError = "No tiene permisos para guardar el archivo INI.\n" + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                mensajeError = "Error inesperado al guardar el archivo INI.\n" + ex.Message;
                return false;
            }
        }

        // Devuelve el indice de la siguiente cabecera de seccion o fin de archivo.
        private int ObtenerIndiceInicioSiguienteSeccion(int indiceInicial)
        {
            for (var i = indiceInicial; i < _lineas.Count; i++)
            {
                if (RegexLineaSeccion.IsMatch(_lineas[i]))
                {
                    return i;
                }
            }

            return _lineas.Count;
        }

        // Separa texto por lineas sin conservar separadores.
        private static IEnumerable<string> SepararLineas(string contenido)
        {
            using (var lector = new StringReader(contenido))
            {
                string linea;
                while ((linea = lector.ReadLine()) != null)
                {
                    yield return linea;
                }
            }
        }

        // Detecta el salto de linea usado por el archivo original.
        private static string DetectarSaltoLinea(string contenido)
        {
            if (contenido.Contains("\r\n"))
            {
                return "\r\n";
            }

            if (contenido.Contains("\n"))
            {
                return "\n";
            }

            return Environment.NewLine;
        }

        // Determina si una seccion debe tratarse como BASCULA o VOLUMETRICO.
        private static TipoSeccionControl DeterminarTipoSeccion(string nombreSeccion)
        {
            var nombreNormalizado = QuitarAcentos(nombreSeccion).ToUpperInvariant();
            if (nombreNormalizado.Contains("BASCULA"))
            {
                return TipoSeccionControl.Bascula;
            }

            if (nombreNormalizado.Contains("VOLUMETRICO"))
            {
                return TipoSeccionControl.Volumetrico;
            }

            return TipoSeccionControl.Desconocido;
        }

        // Quita tildes para comparaciones robustas independientemente de acentos.
        private static string QuitarAcentos(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return string.Empty;
            }

            var descompuesto = texto.Normalize(NormalizationForm.FormD);
            var resultado = new StringBuilder();

            foreach (var caracter in descompuesto)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);
                if (categoria != UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(caracter);
                }
            }

            return resultado.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
