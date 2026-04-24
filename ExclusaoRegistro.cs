namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Snapshot imutavel de um pedido no momento de sua exclusao.
    /// Alimenta a tabela historico_exclusoes. Append-only — nunca atualizado.
    /// </summary>
    public record ExclusaoRegistro(
        int Orcamento,
        string Cliente,
        string NomeFantasia,
        string Rota,
        string Vendedor,
        int PedidoVenda,
        string NotaFiscal,
        string Status,
        bool Aprovado,
        bool EmSeparacao,
        bool Conferido,
        bool Liberado,
        bool Travado,
        bool EmFaturamento,
        string TipoExclusao)
    {
        public const string TipoManual = "MANUAL";
        public const string TipoMassa = "MASSA";

        /// <summary>
        /// Converte o conjunto de flags + pedido_venda em um valor textual
        /// hierarquico representando o estado do pedido no momento da exclusao.
        /// </summary>
        public static string CalcularStatus(
            bool aprovado, bool emSeparacao, bool conferido,
            bool liberado, bool travado, bool emFaturamento, int pedidoVenda)
        {
            if (pedidoVenda > 0) return "FATURADO";
            if (emFaturamento)   return "EM FATURAMENTO";
            if (travado)         return "TRAVADO";
            if (liberado)        return "LIBERADO";
            if (conferido)       return "CONFERIDO";
            if (emSeparacao)     return "EM SEPARACAO";
            if (aprovado)        return "APROVADO";
            return "PENDENTE";
        }
    }
}
