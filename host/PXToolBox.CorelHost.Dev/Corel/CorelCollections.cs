using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    /// <summary>Converte coleções COM do VGCore (1-based) para listas .NET de abstrações de Saídas.</summary>
    internal static class CorelCollections
    {
        public static IReadOnlyList<ICorelShape> ToShapeList(Shapes native)
        {
            var list = new List<ICorelShape>(native.Count);
            for (var i = 1; i <= native.Count; i++)
            {
                list.Add(new CorelShapeAdapter((Shape)native[i]));
            }

            return list;
        }

        public static IReadOnlyList<ICorelShape> ToShapeList(ShapeRange native)
        {
            var list = new List<ICorelShape>(native.Count);
            for (var i = 1; i <= native.Count; i++)
            {
                list.Add(new CorelShapeAdapter((Shape)native[i]));
            }

            return list;
        }

        public static IReadOnlyList<ICorelLayer> ToLayerList(Layers native)
        {
            var list = new List<ICorelLayer>(native.Count);
            for (var i = 1; i <= native.Count; i++)
            {
                list.Add(new CorelLayerAdapter((Layer)native[i]));
            }

            return list;
        }

        public static IReadOnlyList<ICorelPage> ToPageList(Pages native, Document owner)
        {
            var list = new List<ICorelPage>(native.Count);
            for (var i = 1; i <= native.Count; i++)
            {
                list.Add(new CorelPageAdapter((Page)native[i], owner));
            }

            return list;
        }
    }
}
