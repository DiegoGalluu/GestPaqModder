using System;

namespace GestPaqModder.Modelos
{
    // Tipos funcionales de seccion que soporta la herramienta.
    public enum TipoSeccionControl
    {
        Desconocido,
        Bascula,
        Volumetrico
    }

    // Modelo de estado de una seccion del INI mostrada en la UI.
    public class SeccionControl
    {
        public string NombreSeccion { get; set; }

        public string AliasVisible { get; set; }

        public TipoSeccionControl Tipo { get; set; }

        public bool Enable { get; set; }

        public bool TieneEnable { get; set; }

        public int IndiceLineaSeccion { get; set; }

        public int IndiceLineaEnable { get; set; }

        // Texto principal que se enseña en el boton (alias o nombre real).
        public string TextoBotonVisible
        {
            get
            {
                return string.IsNullOrWhiteSpace(AliasVisible)
                    ? "[" + NombreSeccion + "]"
                    : AliasVisible;
            }
        }
    }
}

