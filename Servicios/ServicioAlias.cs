using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GestPaqModder.Servicios
{
    // Entrada individual de alias persistida en JSON.
    [DataContract]
    internal class RegistroAlias
    {
        [DataMember]
        public string IdentificadorIni { get; set; }

        [DataMember]
        public string NombreSeccion { get; set; }

        [DataMember]
        public string AliasVisible { get; set; }
    }

    // Contenedor raiz del archivo de alias.
    [DataContract]
    internal class ContenedorAlias
    {
        [DataMember]
        public List<RegistroAlias> Alias { get; set; } = new List<RegistroAlias>();
    }

    // Servicio de lectura/escritura de alias por INI y seccion.
    public class ServicioAlias
    {
        private readonly string _rutaArchivoAlias;

        // Constructor con ruta de persistencia del JSON.
        public ServicioAlias(string rutaArchivoAlias)
        {
            _rutaArchivoAlias = rutaArchivoAlias;
        }

        // Obtiene los alias aplicables al INI actual.
        public Dictionary<string, string> ObtenerAliasParaIni(string rutaIni)
        {
            var identificadorIni = ConstruirIdentificadorIni(rutaIni);
            var contenedor = CargarContenedorAlias();

            // Si existen duplicados historicos, nos quedamos con el ultimo.
            return contenedor.Alias
                .Where(a => string.Equals(a.IdentificadorIni, identificadorIni, StringComparison.OrdinalIgnoreCase))
                .GroupBy(a => a.NombreSeccion, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().AliasVisible, StringComparer.OrdinalIgnoreCase);
        }

        // Guarda/actualiza/elimina un alias para una seccion concreta.
        public bool GuardarAlias(string rutaIni, string nombreSeccion, string aliasVisible, out string mensajeError)
        {
            mensajeError = null;

            if (string.IsNullOrWhiteSpace(rutaIni))
            {
                mensajeError = "No hay una ruta INI cargada para asociar alias.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(nombreSeccion))
            {
                mensajeError = "Nombre de seccion invalido para alias.";
                return false;
            }

            try
            {
                var identificadorIni = ConstruirIdentificadorIni(rutaIni);
                var contenedor = CargarContenedorAlias();
                var existente = contenedor.Alias.FirstOrDefault(
                    a => string.Equals(a.IdentificadorIni, identificadorIni, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(a.NombreSeccion, nombreSeccion, StringComparison.OrdinalIgnoreCase));

                var aliasNormalizado = string.IsNullOrWhiteSpace(aliasVisible) ? string.Empty : aliasVisible.Trim();

                // Alias vacio significa "quitar personalizacion" y volver al nombre real.
                if (string.IsNullOrWhiteSpace(aliasNormalizado))
                {
                    if (existente != null)
                    {
                        contenedor.Alias.Remove(existente);
                    }
                }
                else
                {
                    if (existente == null)
                    {
                        existente = new RegistroAlias
                        {
                            IdentificadorIni = identificadorIni,
                            NombreSeccion = nombreSeccion,
                            AliasVisible = aliasNormalizado
                        };
                        contenedor.Alias.Add(existente);
                    }
                    else
                    {
                        existente.AliasVisible = aliasNormalizado;
                    }
                }

                GuardarContenedorAlias(contenedor);
                return true;
            }
            catch (IOException ex)
            {
                mensajeError = "No se pudo guardar el archivo de alias.\n" + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                mensajeError = "No tiene permisos para guardar el archivo de alias.\n" + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                mensajeError = "Error inesperado al guardar alias.\n" + ex.Message;
                return false;
            }
        }

        // Lee el JSON de alias de disco.
        private ContenedorAlias CargarContenedorAlias()
        {
            try
            {
                if (!File.Exists(_rutaArchivoAlias))
                {
                    return new ContenedorAlias();
                }

                using (var flujo = File.OpenRead(_rutaArchivoAlias))
                {
                    if (flujo.Length == 0)
                    {
                        return new ContenedorAlias();
                    }

                    var serializador = new DataContractJsonSerializer(typeof(ContenedorAlias));
                    var objeto = serializador.ReadObject(flujo) as ContenedorAlias;
                    return objeto ?? new ContenedorAlias();
                }
            }
            catch
            {
                // Si el JSON esta corrupto, no bloqueamos la app: arrancamos sin alias.
                return new ContenedorAlias();
            }
        }

        // Escribe el contenedor completo de alias en disco.
        private void GuardarContenedorAlias(ContenedorAlias contenedor)
        {
            var carpeta = Path.GetDirectoryName(_rutaArchivoAlias);
            if (!string.IsNullOrWhiteSpace(carpeta) && !Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            using (var flujo = File.Create(_rutaArchivoAlias))
            {
                var serializador = new DataContractJsonSerializer(typeof(ContenedorAlias));
                serializador.WriteObject(flujo, contenedor);
            }
        }

        // Genera identificador estable para asociar alias a un INI concreto.
        private static string ConstruirIdentificadorIni(string rutaIni)
        {
            return Path.GetFullPath(rutaIni).Trim().ToUpperInvariant();
        }
    }
}
