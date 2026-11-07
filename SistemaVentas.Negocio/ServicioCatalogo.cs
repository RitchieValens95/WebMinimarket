using System;
using System.Linq;
using System.Text;
using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public abstract class ServicioCatalogo
    {
        private readonly string cadena;
        private readonly TipoCatalogo tipo;
        private readonly string permiso;
        protected ServicioCatalogo(string conexion,TipoCatalogo catalogo)
        {
            cadena=conexion;tipo=catalogo;
            switch(tipo)
            {
                case TipoCatalogo.Categorias:permiso="CATEGORIAS_ADMINISTRAR";break;
                case TipoCatalogo.Marcas:permiso="MARCAS_ADMINISTRAR";break;
                default:throw new ArgumentOutOfRangeException("catalogo");
            }
        }
        private RepositorioCatalogo Abrir()
        { return tipo==TipoCatalogo.Categorias?(RepositorioCatalogo)new CategoriasRepositorio(cadena):new MarcasRepositorio(cadena); }
        private void Exigir(RepositorioCatalogo db,long actor)
        {
            if(!db.Puede(actor,permiso)) throw new OperacionNoPermitidaException("No tienes permiso para administrar este catálogo.");
        }
        public PaginaCatalogo Listar(long actor,int pagina=1,string busqueda="",string estado="todos")
        {
            busqueda=(busqueda??"").Trim(); if(busqueda.Length>100) busqueda=busqueda.Substring(0,100);
            estado=(estado??"todos").ToLowerInvariant();
            if(estado!="todos" && estado!="activos" && estado!="inactivos") estado="todos";
            using(var db=Abrir())
            {
                Exigir(db,actor);
                return db.Listar(Math.Max(1,Math.Min(pagina,1000000)),busqueda,estado);
            }
        }
        public RegistroCatalogo Obtener(long actor,long id)
        {
            using(var db=Abrir()) { Exigir(db,actor);return db.Obtener(id); }
        }
        public long Guardar(long actor,long id,string nombre,bool activo,long version)
        {
            nombre=(nombre??"").Trim().Normalize(NormalizationForm.FormC);
            if(id<0 || nombre.Length==0 || nombre.Length>100 || nombre.Any(char.IsControl))
                throw new OperacionNoPermitidaException("Escribe un nombre de 1 a 100 caracteres, sin saltos de línea ni caracteres de control.");
            using(var db=Abrir())
            {
                Exigir(db,actor);db.ExigirVersion();
                if(id==0)
                {
                    id=db.Insertar(nombre,activo);
                    db.Auditar(actor,tipo==TipoCatalogo.Categorias?"CATEGORIA_CREADA":"MARCA_CREADA",id);
                }
                else
                {
                    var actual=db.Obtener(id,true);
                    ValidarActual(actual,version);
                    if(actual.Nombre==nombre && actual.Activo==activo) return id;
                    db.Actualizar(id,nombre,activo,version);
                    db.Auditar(actor,tipo==TipoCatalogo.Categorias?"CATEGORIA_EDITADA":"MARCA_EDITADA",id);
                }
                db.Confirmar();return id;
            }
        }
        public void Desactivar(long actor,long id,long version)
        {
            using(var db=Abrir())
            {
                Exigir(db,actor);db.ExigirVersion();
                var actual=db.Obtener(id,true);ValidarActual(actual,version);
                if(!actual.Activo) return;
                db.Actualizar(id,actual.Nombre,false,version);
                db.Auditar(actor,tipo==TipoCatalogo.Categorias?"CATEGORIA_DESACTIVADA":"MARCA_DESACTIVADA",id);
                db.Confirmar();
            }
        }
        private static void ValidarActual(RegistroCatalogo actual,long version)
        {
            if(actual==null) throw new OperacionNoPermitidaException("No se encontró el registro.");
            if(version<=0 || actual.Version!=version) throw new OperacionNoPermitidaException("El registro cambió en otra ventana. Recarga la página antes de volver a guardar.");
        }
    }
}
