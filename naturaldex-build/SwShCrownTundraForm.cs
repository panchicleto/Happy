using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
namespace NaturalDex.Plugin;
internal sealed class SwShCrownTundraForm : Form
{
 private readonly SAV8SWSH _sav;
 private readonly Label _status=new(){AutoSize=true,Text="Selecciona una operación. Se crea backup antes de aplicar."};
 private static readonly uint[] MaxLair={0xF75E52CF,0xF75E5635,0xF75E511C,0xF75E4DB6,0xF75E4C03,0xF75E4A50,0xF75E4F69,0xF75E621A,0xF75E63CD,0xF760963E,0xF76097F1,0xF7609B57,0xF760948B,0xF76092D8,0xF76086F3,0xF7608540,0xF7582170,0xF76099A4,0xF7609D0A,0xF7609EBD,0xF7582323,0xF75824D6,0xF7582BA2,0xF7582D55,0xF7582F08,0xF7582689,0xF758283C,0xF75829EF,0xF75830BB,0xF75B3AF9,0xF75B3946,0xF75B3E5F,0xF75B3CAC,0xF75B3793,0xF75B35E0,0xF75B41C5,0xF75B4012,0xF75B46DE,0xF769AAC6,0xF769AC79,0xF769A760,0xF769B192,0xF769A913,0xF769B345,0xF75B4891,0xF769B85E,0xF769AFDF};
 private static readonly uint[] Regis={0xEE3F84E6,0xDAB3DD3A,0xEE1FD86E,0xC4308A93,0x4F4AEC32,0x4F30F174};
 private static readonly uint[] Swords={0xBB305227,0x750C83A4,0x1A27DF2C,0xA097DE31};
 private static readonly uint[] Footprints={0x4D50B655,0x771E4C88,0xAD67A297};
 private static readonly uint[] Notes={0x6F669A35,0x6F66951C,0x6F6696CF,0xF26B9151};
 private const uint RegiPattern=0xCF90B39A;
 public SwShCrownTundraForm(SAV8SWSH sav){_sav=sav;Text="NaturalDex — Crown Tundra Reset";StartPosition=FormStartPosition.CenterParent;MinimumSize=new Size(620,360);var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14),AutoScroll=true};p.Controls.Add(new Label{AutoSize=true,Font=new Font(Font,FontStyle.Bold),Text="Sword/Shield — Crown Tundra / Dynamax Adventure Reset"});p.Controls.Add(Make("RESETEAR CAPTURAS DE AVENTURAS DINAMAX",()=>Apply("Aventuras Dinamax",()=>Flags(MaxLair,false))));p.Controls.Add(Make("LIMPIAR 3 NOTAS + PISTA DE PEONIA",()=>Apply("Notas Max Lair",()=>Values(Notes,0))));p.Controls.Add(Make("RESETEAR REGIS + ELEKI/DRAGO",()=>Apply("Regis",()=>{Flags(Regis,false);_sav.Blocks.GetBlock(RegiPattern).SetValue(0u);})));p.Controls.Add(Make("RESETEAR ESPADAS DE LA JUSTICIA + HUELLAS",()=>Apply("Espadas de la Justicia",()=>{Flags(Swords,false);Values(Footprints,0);})));p.Controls.Add(Make("RESETEAR TODO CROWN TUNDRA",()=>Apply("Crown Tundra completo",()=>{Flags(MaxLair,false);Values(Notes,0);Flags(Regis,false);_sav.Blocks.GetBlock(RegiPattern).SetValue(0u);Flags(Swords,false);Values(Footprints,0);})));p.Controls.Add(_status);Controls.Add(p);}
 private static Button Make(string t,Action a){var b=new Button{Text=t,AutoSize=true,MinimumSize=new Size(520,36)};b.Click+=(_,__)=>a();return b;}
 private void Apply(string name,Action action){if(MessageBox.Show($"¿Aplicar '{name}'? Se creará un backup primero.","NaturalDex",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;SaveFile snap=_sav.Clone();string backup;try{backup=SaveBackupManager.Create(_sav);}catch(Exception ex){MessageBox.Show("No se pudo crear backup. No se modificó el save.\n\n"+ex.Message);return;}try{action();_sav.State.Edited=true;_status.Text=name+" reseteado.";MessageBox.Show(name+" reseteado.\n\nBackup:\n"+backup,"NaturalDex",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){_sav.CopyChangesFrom(snap);MessageBox.Show("Falló y se restauró el snapshot.\n\n"+ex.GetBaseException().Message,"NaturalDex",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
 private void Flags(IEnumerable<uint> keys,bool value){var type=value?SCTypeCode.Bool2:SCTypeCode.Bool1;foreach(uint k in keys)_sav.Blocks.GetBlock(k).ChangeBooleanType(type);}
 private void Values(IEnumerable<uint> keys,uint value){foreach(uint k in keys)_sav.Blocks.GetBlock(k).SetValue(value);}
}