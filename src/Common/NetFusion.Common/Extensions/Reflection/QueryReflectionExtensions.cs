﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetFusion.Common.Extensions.Reflection
{
    public static class QueryReflectionExtensions
    {
        /// <summary>
        /// Determines if the type is a non-abstract type derived from the specified parent type.
        /// </summary>
        /// <param name="type">The type to test if a derived concrete type.</param>
        /// <param name="parentType">The possible parent type of the child type.</param>
        /// <returns>True if the child type is not abstract and is derived from the parent type.</returns>
        public static bool IsConcreteTypeDerivedFrom(this Type type, Type parentType)
        {
            Check.NotNull(type, nameof(type));
            Check.NotNull(parentType, nameof(parentType));

            var childTypeInfo = type.GetTypeInfo();

            return type.IsDerivedFrom(parentType) && !childTypeInfo.IsAbstract;
        }

        /// <summary>
        /// Determines if the type is a non-abstract type derived from the specified parent type.
        /// </summary>
        /// <typeparam name="T">The possible parent type of the child type.</typeparam>
        /// <param name="childType">TThe type to test if a derived concrete type.</param>
        /// <returns>True if the type derives from the specified base type otherwise false.</returns>
        public static bool IsConcreteTypeDerivedFrom<T>(this Type childType)
        {
            Check.NotNull(childType, nameof(childType));
            return IsConcreteTypeDerivedFrom(childType, typeof(T));
        }

        /// <summary>
        /// Determines if a type derives from a base type.
        /// </summary>
        /// <typeparam name="T">The parent type.</typeparam>
        /// <param name="childType">The child type to check.</param>
        /// <returns>True if the type derives from the specified base type otherwise false.</returns>
        public static bool IsDerivedFrom<T>(this Type childType)
        {
            Check.NotNull(childType, nameof(childType));
            return typeof(T).IsAssignableFrom(childType) && childType != typeof(T);
        }

        /// <summary>
        /// Determines if a type is assignable to a parent type.
        /// </summary>
        /// <param name="childType">The child type to check.</param>
        /// <param name="parentType">The parent type.</param>
        /// <returns>True if the type derives from the specified base type otherwise false.</returns>
        public static bool IsDerivedFrom(this Type childType, Type parentType)
        {
            Check.NotNull(childType, nameof(childType));
            Check.NotNull(parentType, nameof(parentType));

            return parentType.IsAssignableFrom(childType) && childType != parentType;
        }

        /// <summary>
        /// Determines if a child type can be assigned to a specified parent type.
        /// </summary>
        /// <param name="childType">The child type to test.</param>
        /// <param name="parentType">The parent type to test.</param>
        /// <returns>True if the child type is assignable to the parent type.  Otherwise, False.</returns>
        public static bool CanAssignTo(this Type childType, Type parentType)
        {
            Check.NotNull(childType, nameof(childType));
            Check.NotNull(parentType, nameof(parentType));

            return parentType.IsAssignableFrom(childType);
        }

        /// <summary>
        ///  Determines if a child type can be assigned to a specified parent type.
        /// </summary>
        /// <typeparam name="T">The parent type to test</typeparam>
        /// <param name="childType">The child type to test.</param>
        /// <returns>True if the child type is assignable to the parent type.  Otherwise, False.</returns>
        public static bool CanAssignTo<T>(this Type childType)
        {
            Check.NotNull(childType, nameof(childType));
            return typeof(T).IsAssignableFrom(childType);
        }

        /// <summary>
        /// Returns all interfaces of a type deriving from a specified interface.
        /// </summary>
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="type">The type to check for interfaces.</param>
        /// <returns>Returns the list of matching interfaces.</returns>
        public static IEnumerable<Type> GetInterfacesDerivedFrom<T>(this Type type)
        {
            Check.NotNull(type, nameof(type));

            if (!typeof(T).GetTypeInfo().IsInterface)
            {
                throw new InvalidOperationException(
                    "The specified generic parameter must be an interface type.");
            }

            return type.GetTypeInfo()
                .GetInterfaces()
                .Where(mi => mi.IsDerivedFrom<T>());
        }

        /// <summary>
        /// Determines if a specified type has a default constructor.
        /// </summary>
        /// <param name="type">The type to verify.</param>
        /// <returns>True if the type has an empty constructor.  Otherwise, false.</returns>
        public static bool HasDefaultConstructor(this Type type)
        {
            Check.NotNull(type, nameof(type));

            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsValueType || typeInfo.GetConstructor(Type.EmptyTypes) != null;
        }

        public static Type[] GetParameterTypes(this MethodInfo methodInfo)
        {
            return methodInfo.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();
        }
    }
}
