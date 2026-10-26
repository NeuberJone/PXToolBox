<?xml version="1.0"?>
<!--
PX Tool Box DEV — adiciona o item de alternância do docker ao menu Janela > Janelas de encaixe.

Correção (branch fix/dev-docker-hosting): a versão anterior adicionava um item à barra de
comandos padrão (Standard toolbar) referenciando diretamente o `wpfhost`, o que forçava o
controle a renderizar embutido na barra, comprimido ao lado do SISBolt. Agora o item referenciado
é o `checkButton` declarado em AppUI.xslt (guid "0dd0d869-201a-40a9-b3d0-801f9138f700"), que
alterna o docker real — o mesmo padrão usado pelo MoldMotor e pelo exemplo oficial do SDK da
Corel (eCutDocker), ambos já instalados nesta máquina.

Nota: este arquivo só é reaplicado quando o CorelDRAW recria a configuração de workspace do
usuário (reinício do Corel, ou F8 dentro do Corel para "resetar workspace"). Se o item não
aparecer depois de instalar, reinicie o CorelDRAW (ver docs/mvp-smoke-test.md).

O guid "3eaa9bbe-28fd-4672-9128-02974ee96332" referenciado abaixo é um identificador FIXO do
CorelDRAW para o menu "Janela > Janelas de encaixe" — não foi inventado por este add-on; é o
mesmo valor usado pelo MoldMotor e pelo eCutDocker para o mesmo fim.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:frmwrk="Corel Framework Data"
                exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='3eaa9bbe-28fd-4672-9128-02974ee96332']"/>
    <frmwrk:compositeNode xPath="/uiConfig/frame"/>
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <!-- Acrescenta o item ao final do menu "Janelas de encaixe". -->
  <xsl:template match="commandBarData[@guid='3eaa9bbe-28fd-4672-9128-02974ee96332']/menu">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='0dd0d869-201a-40a9-b3d0-801f9138f700'])">
        <item guidRef="0dd0d869-201a-40a9-b3d0-801f9138f700"/>
      </xsl:if>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
