﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Этот код создан программой.
//     Исполняемая версия:4.0.30319.42000
//
//     Изменения в этом файле могут привести к неправильной работе и будут потеряны в случае
//     повторной генерации кода.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Net.Leksi.Edifact.Properties {
    using System;
    
    
    /// <summary>
    ///   Класс ресурса со строгой типизацией для поиска локализованных строк и т.д.
    /// </summary>
    // Этот класс создан автоматически классом StronglyTypedResourceBuilder
    // с помощью такого средства, как ResGen или Visual Studio.
    // Чтобы добавить или удалить член, измените файл .ResX и снова запустите ResGen
    // с параметром /str или перестройте свой проект VS.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Возвращает кэшированный экземпляр ResourceManager, использованный этим классом.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Net.Leksi.Edifact.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Перезаписывает свойство CurrentUICulture текущего потока для всех
        ///   обращений к ресурсу с помощью этого класса ресурса со строгой типизацией.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;system_segments.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;BATCH_INTERCHANGE&quot;&gt;
        ///    &lt;xs:sequence&gt;
        ///      &lt;xs:element name=&quot;UNB&quot; type=&quot;UNB&quot;/&gt;
        ///      &lt;xs:choic [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string batch_interchange {
            get {
                return ResourceManager.GetString("batch_interchange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;elements.xsd&quot;/&gt;
        ///&lt;/xs:schema&gt;.
        /// </summary>
        internal static string composites {
            get {
                return ResourceManager.GetString("composites", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///&gt;
        ///  &lt;xs:complexType name=&quot;BASE-MESSAGE&quot; abstract=&quot;true&quot;&gt;
        ///    &lt;xs:sequence id=&quot;structure&quot;/&gt;
        ///  &lt;/xs:complexType&gt;
        ///  
        ///  &lt;xs:complexType name=&quot;BASE-SEGMENT&quot; abstract=&quot;true&quot;&gt;
        ///     [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string edifact {
            get {
                return ResourceManager.GetString("edifact", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на http://leksi.net/EDIFACT.
        /// </summary>
        internal static string edifact_ns {
            get {
                return ResourceManager.GetString("edifact_ns", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на http://leksi.net/EDIFACT/script.
        /// </summary>
        internal static string edifact_script_ns {
            get {
                return ResourceManager.GetString("edifact_script_ns", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на http://leksi.net/EDIFACT/utility.
        /// </summary>
        internal static string edifact_utility_ns {
            get {
                return ResourceManager.GetString("edifact_utility_ns", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;../../edifact.xsd&quot; /&gt;
        ///  &lt;xs:complexType name=&quot;D0081&quot;&gt;
        ///    &lt;xs:simpleContent&gt;
        ///      &lt;xs:restrictio [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string elements {
            get {
                return ResourceManager.GetString("elements", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;system_segments.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;INTERACTIVE_INTERCHANGE&quot;&gt;
        ///    &lt;xs:sequence&gt;
        ///      &lt;xs:element name=&quot;UIB&quot; type=&quot;UIB&quot;/&gt;
        ///      &lt;xs [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string interactive_interchange {
            get {
                return ResourceManager.GetString("interactive_interchange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;segments.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;MESSAGE&quot;&gt;
        ///    &lt;xs:annotation&gt;
        ///      &lt;xs:appinfo eu:name=&quot; [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string message {
            get {
                return ResourceManager.GetString("message", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на http://www.w3.org/2001/XMLSchema-instance.
        /// </summary>
        internal static string schema_instance_ns {
            get {
                return ResourceManager.GetString("schema_instance_ns", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на http://www.w3.org/2001/XMLSchema.
        /// </summary>
        internal static string schema_ns {
            get {
                return ResourceManager.GetString("schema_ns", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;composites.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;UNS&quot;&gt;
        ///    &lt;xs:annotation&gt;
        ///      &lt;xs:documentation eu:na [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string segments {
            get {
                return ResourceManager.GetString("segments", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;system_elements.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;S001&quot;&gt;
        ///    &lt;xs:annotation&gt;
        ///      &lt;xs:documentation [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string system_composites {
            get {
                return ResourceManager.GetString("system_composites", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;edifact.xsd&quot; /&gt;
        ///  &lt;xs:complexType name=&quot;D0001&quot;&gt;
        ///    &lt;xs:simpleContent&gt;
        ///      &lt;xs:restriction base [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string system_elements {
            get {
                return ResourceManager.GetString("system_elements", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;!-- UN --&gt;
        ///&lt;!-- The file was automatically generated --&gt;
        ///&lt;!-- Don&apos;t edit! --&gt;
        ///&lt;xs:schema
        ///  targetNamespace=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns=&quot;http://leksi.net/EDIFACT&quot;
        ///  xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///  elementFormDefault=&quot;qualified&quot; attributeFormDefault=&quot;unqualified&quot;
        ///  xmlns:eu=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///&gt;
        ///  &lt;xs:include schemaLocation=&quot;system_composites.xsd&quot;/&gt;
        ///  &lt;xs:complexType name=&quot;UNB&quot;&gt;
        ///    &lt;xs:annotation&gt;
        ///      &lt;xs:documentatio [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string system_segments {
            get {
                return ResourceManager.GetString("system_segments", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Ищет локализованную строку, похожую на &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;xs:schema id=&quot;utility&quot;
        ///    targetNamespace=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///    elementFormDefault=&quot;qualified&quot;
        ///    xmlns=&quot;http://leksi.net/EDIFACT/utility&quot;
        ///    xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot;
        ///&gt;
        ///  &lt;xs:attribute name=&quot;name&quot;&gt;
        ///    &lt;xs:simpleType&gt;
        ///      &lt;xs:restriction base=&quot;xs:string&quot;&gt;
        ///        &lt;xs:enumeration value=&quot;name&quot;&gt;
        ///          &lt;xs:annotation&gt;
        ///            &lt;xs:documentation&gt;Used at annotations for segment/composite/element name&lt;/xs:documentat [остаток строки не уместился]&quot;;.
        /// </summary>
        internal static string utility {
            get {
                return ResourceManager.GetString("utility", resourceCulture);
            }
        }
    }
}
