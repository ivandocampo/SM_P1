using System.Collections.Generic;
using UnityEngine;

public partial class GuardAgent
{
    private static GUIStyle estiloDebugPanel;
    private static GUIStyle estiloDebugTitulo;
    private static List<GuardAgent> guardiasDebug = new List<GuardAgent>();

    private Color ColorPorBehavior(BehaviorType tipo)
    {
        switch (tipo)
        {
            case BehaviorType.Pursuit: return new Color(0.85f, 0.12f, 0.12f);
            case BehaviorType.Intercept: return new Color(0.95f, 0.65f, 0.08f);
            case BehaviorType.Search:
            case BehaviorType.SearchAssigned: return new Color(0.1f, 0.35f, 0.85f);
            case BehaviorType.BlockExit: return new Color(0.45f, 0.15f, 0.75f);
            case BehaviorType.CheckPedestal: return new Color(0.9f, 0.9f, 0.9f);
            case BehaviorType.Patrol: return new Color(0.1f, 0.55f, 0.2f);
            default: return new Color(0.25f, 0.25f, 0.25f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!mostrarMarcadorDebug) return;

        Gizmos.color = Application.isPlaying ? ColorPorBehavior(behaviorActivo_tipo) : Color.cyan;
        Vector3 centro = transform.position + Vector3.up * 1.8f;
        Gizmos.DrawSphere(centro, 0.45f);
        Gizmos.DrawLine(transform.position, centro);
    }

    private void DibujarPanelDebug()
    {
        if (!mostrarDebugEnPantalla || guardiasDebug.Count == 0) return;

        List<GuardAgent> ordenados = new List<GuardAgent>(guardiasDebug);
        ordenados.RemoveAll(g => g == null);
        ordenados.Sort((a, b) => string.Compare(a.agentId, b.agentId, System.StringComparison.Ordinal));
        if (ordenados.Count == 0 || ordenados[0] != this) return;

        if (estiloDebugPanel == null)
        {
            estiloDebugPanel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            estiloDebugPanel.normal.textColor = Color.white;
        }

        if (estiloDebugTitulo == null)
        {
            estiloDebugTitulo = new GUIStyle(estiloDebugPanel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            estiloDebugTitulo.normal.textColor = Color.white;
        }

        float x = posicionPanelDebug.x;
        float y = posicionPanelDebug.y;
        float ancho = anchoPanelDebug;
        float alto = 34f + ordenados.Count * 23f;

        Color anterior = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.95f);
        GUI.Box(new Rect(x, y, ancho, alto), GUIContent.none);
        GUI.color = anterior;

        GUI.Label(new Rect(x + 10f, y + 6f, ancho - 20f, 22f), "DEBUG GUARDIAS", estiloDebugTitulo);

        for (int i = 0; i < ordenados.Count; i++)
        {
            GuardAgent guardia = ordenados[i];
            if (guardia == null || guardia.creencias == null) continue;

            string zona = guardia.creencias.TareaAsignada != null ? guardia.creencias.TareaAsignada.ZoneId : "-";
            string linea = $"{AbreviarAgente(guardia.agentId),-4} {AbreviarBehavior(guardia.behaviorActivo_tipo),-10} {AbreviarFase(guardia.creencias.FaseActual()),-13} Z:{zona}";

            GUI.color = guardia.ColorPorBehavior(guardia.behaviorActivo_tipo);
            GUI.DrawTexture(new Rect(x + 10f, y + 38f + i * 23f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 32f, y + 31f + i * 23f, ancho - 42f, 22f), linea, estiloDebugPanel);
        }

        GUI.color = anterior;
    }

    private static string AbreviarFase(TacticalPhase fase)
    {
        switch (fase)
        {
            case TacticalPhase.NormalPatrol: return "Normal";
            case TacticalPhase.RingSafeThiefKnown: return "SinAnillo/V";
            case TacticalPhase.RingSafeThiefLost: return "SinAnillo/P";
            case TacticalPhase.RingStolenThiefKnown: return "Anillo/V";
            case TacticalPhase.RingStolenThiefLost: return "Anillo/P";
            default: return fase.ToString();
        }
    }

    private static string AbreviarBehavior(BehaviorType tipo)
    {
        switch (tipo)
        {
            case BehaviorType.Patrol: return "Patrol";
            case BehaviorType.Pursuit: return "Pursuit";
            case BehaviorType.Search: return "Search";
            case BehaviorType.SearchAssigned: return "SearchZone";
            case BehaviorType.Intercept: return "Intercept";
            case BehaviorType.BlockExit: return "BlockExit";
            case BehaviorType.CheckPedestal: return "Pedestal";
            case BehaviorType.None: return "Deciding";
            default: return tipo.ToString();
        }
    }

    private static string AbreviarAgente(string id)
    {
        if (string.IsNullOrEmpty(id)) return "G?";

        int guion = id.LastIndexOf('_');
        if (guion >= 0 && guion < id.Length - 1)
            return "G" + id.Substring(guion + 1);

        return id.Length <= 4 ? id : id.Substring(0, 4);
    }
}
