namespace EntityFrameworkCore.Projectables
{
    /// <summary>
    /// Declares this property, method or constructor to be Projectable. 
    /// A companion Expression tree will be generated
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = true, AllowMultiple = false)]
    public sealed class ProjectableAttribute : Attribute
    {
        /// <summary>
        /// Get or set how null-conditional operators are handeled
        /// </summary>
        public NullConditionalRewriteSupport NullConditionalRewriteSupport { get; set; }

        /// <summary>
        /// Get or set from which member to get the expression,
        /// or null to get it from the current member.
        /// </summary>
        public string? UseMemberBody { get; set; }

        /// <summary>
        /// Get or set whether to expand enum method/extension calls by evaluating them and generating ternary
        /// expressions for each enum value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When enabled, method calls on enum values are rewritten into a chain of ternary expressions that call
        /// the method for each possible enum value.
        /// </para>
        /// <para>
        /// For example, <c>MyEnumValue.GetDescription()</c> would be expanded to:
        /// <c>MyEnumValue == MyEnum.Value1 ? MyEnum.Value1.GetDescription() :
        /// MyEnumValue == MyEnum.Value2 ? MyEnum.Value2.GetDescription() : null</c>
        /// </para>
        /// <para>
        /// This is useful for <c>Where()</c> and <c>OrderBy()</c> clauses where the expression
        /// needs to be translated to SQL.
        /// </para>
        /// </remarks>
        public bool ExpandEnumMethods { get; set; }

        /// <summary>
        /// Get or set whether to allow block-bodied members (experimental feature).
        /// </summary>
        /// <remarks>
        /// Block-bodied method support is experimental and may have limitations.
        /// Set this to true to suppress the experimental feature warning.
        /// </remarks>
        public bool AllowBlockBody { get; set; }

        /// <summary>
        /// Get or set whether to inline derived types overrides of the member.
        /// </summary>
        public bool PolymorphicDispatch { get; set; }
    }
}
