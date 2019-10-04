using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FreeDraw
{
    // 1. Attach this to a read/write enabled sprite image
    // 2. Set the drawing_layers  to use in the raycast
    // 3. Attach a 2D collider (like a Box Collider 2D) to this sprite
    // 4. Hold down left mouse to draw on this texture!
    public class Drawable : NetworkBehaviour
    {
        //process
        public enum Process
        {
            start,
            creatName,
            action,
            wait,
            end
        }

        public class PlayerInfo
        {
            public string name;
            public int id;

            public PlayerInfo(string name, int id)
            {
                this.name = name;
                this.id = id;
            }
        }
        // PEN COLOUR
        public static Color Pen_Colour = Color.red;     // Change these to change the default drawing settings
        // PEN WIDTH (actually, it's a radius, in pixels)
        public static int Pen_Width = 3;


        public delegate void Brush_Function(Vector2 world_position);
        // This is the function called when a left click happens
        // Pass in your own custom one to change the brush type
        // Set the default function in the Awake method
        public Brush_Function current_brush;

        public LayerMask Drawing_Layers;

        public bool Reset_Canvas_On_Play = true;
        // The colour the canvas is reset to each time
        public Color Reset_Colour = new Color(0, 0, 0, 0);  // By default, reset the canvas to be transparent

        // Used to reference THIS specific file without making all methods static
        public static Drawable drawable;
        // MUST HAVE READ/WRITE enabled set in the file editor of Unity
        Sprite drawable_sprite;
        Texture2D drawable_texture;

        Vector2 previous_drag_position;
        Color[] clean_colours_array;
        Color transparent;
        Color32[] cur_colors;
        bool mouse_was_previously_held_down = false;
        bool no_drawing_on_current_drag = false;

        //setting
        public static bool isCursorOverUI = false;
        public float Transparency = 1f;

        //UI
        Button btn_clear;
        Button btn_set_red;
        Button btn_set_Green;
        Button btn_set_Blue;
        Button btn_set_Eraser;
        Button btn_Enter;
        Button[] btn_shading = new Button[4];
        Slider slider_width;
        Slider slider_trans;
        Text sysText;
        Text ansText;
        Text chatText;
        Text scoreText;
        InputField inputField;
        GameObject shadingItem;
        public Image ImgPlayer1;
        public Image ImgPlayer2;
        public List <PlayerInfo> playerInfoList = new List <PlayerInfo>();
        public Image shadingImage;
        public Sprite shadingSprite;

        private float shadingTime;
        private bool anyBuff = false;

        //Sync
        [SyncVar]
        public Process process = Process.start;
        [SyncVar]
        public string sysMsg;
        [SyncVar]
        public string sysAns;
        [SyncVar]
        public int score = 0;
        [SyncVar]
        public string sysChat;
        [SyncVar]
        public string sysScore;
        [SyncVar]
        public string name;
        [SyncVar]
        public int playersNum;
        //info
        GM gm;
        [SyncVar]
        public int id;

        


        //process
        public void SetProcess(Process process)
        {
            this.process = process;
        }

        //////////////////////////////////////////////////////////////////////////////
        // BRUSH TYPES. Implement your own here


        // When you want to make your own type of brush effects,
        // Copy, paste and rename this function.
        // Go through each step
        public void BrushTemplate(Vector2 world_position)
        {
            // 1. Change world position to pixel coordinates
            Vector2 pixel_pos = WorldToPixelCoordinates(world_position);

            // 2. Make sure our variable for pixel array is updated in  this frame
            cur_colors = drawable_texture.GetPixels32();

            ////////////////////////////////////////////////////////////////
            // FILL IN CODE BELOW HERE

            // Do we care about the user left clicking and dragging?
            // If you don't, simply set the below if statement to be:
            // if (true)
            if (previous_drag_position == Vector2.zero)
            {
                // THIS IS THE FIRST CLICK
                // FILL IN WHATEVER YOU WANT TO DO HERE
                // Maybe mark multiple pixels to colour?
                MarkPixelsToColour(pixel_pos, Pen_Width, Pen_Colour);
            }
            else
            {
                // THE USER IS DRAGGING
                // Should we do stuff between the rpevious mouse position and the current one?
                ColourBetween(previous_drag_position, pixel_pos, Pen_Width, Pen_Colour);
            }
            ////////////////////////////////////////////////////////////////

            // 3. Actually apply the changes we marked earlier
            // Done here to be more efficient
            ApplyMarkedPixelChanges();

            // 4. If dragging, update where we were previously
            previous_drag_position = pixel_pos;
        }




        // Default brush type. Has width and colour.
        // Pass in a point in WORLD coordinates
        // Changes the surrounding pixels of the world_point to the static pen_colour

        public void PenBrush(Vector2 world_point)
        {
            Vector2 pixel_pos = WorldToPixelCoordinates(world_point);

            cur_colors = drawable_texture.GetPixels32();

            if (previous_drag_position == Vector2.zero)
            {
                // If this is the first time we've ever dragged on this image, simply colour the pixels at our mouse position
                MarkPixelsToColour(pixel_pos, Pen_Width, Pen_Colour);
            }
            else
            {
                // Colour in a line from where we were on the last update call
                ColourBetween(previous_drag_position, pixel_pos, Pen_Width, Pen_Colour);
            }
            ApplyMarkedPixelChanges();

            //Debug.Log("Dimensions: " + pixelWidth + "," + pixelHeight + ". Units to pixels: " + unitsToPixels + ". Pixel pos: " + pixel_pos);
            previous_drag_position = pixel_pos;
        }


        // Helper method used by UI to set what brush the user wants
        // Create a new one for any new brushes you implement
        public void SetPenBrush()
        {
            // PenBrush is the NAME of the method we want to set as our current brush
            current_brush = PenBrush;
        }
        //////////////////////////////////////////////////////////////////////////////



        [Command]
        void CmdSendPoint(Vector2 world_point)
        {
            RpcGetPoint(world_point);
        }

        [ClientRpc]
        void RpcGetPoint(Vector2 world_point)
        {
            current_brush(world_point);
        }

        [Command]
        void Cmd_set_previous_drag_position_to_zero()
        {
            Rpc_set_previous_drag_position_to_zero();
        }

        [ClientRpc]
        void Rpc_set_previous_drag_position_to_zero()
        {
            previous_drag_position = Vector2.zero;
        }



        // This is where the magic happens.
        // Detects when user is left clicking, which then call the appropriate function
        void Update()
        {
            //Debug.Log(previous_drag_position);
            if (!isLocalPlayer)
                return;
            // Is the user holding down the left mouse button?
            if (process == Process.action)
            {
                bool mouse_held_down = Input.GetMouseButton(0);
                if (mouse_held_down && !no_drawing_on_current_drag)
                {
                    // Convert mouse coordinates to world coordinates
                    Vector2 mouse_world_position = Camera.main.ScreenToWorldPoint(Input.mousePosition);

                    // Check if the current mouse position overlaps our image
                    Collider2D hit = Physics2D.OverlapPoint(mouse_world_position, Drawing_Layers.value);
                    if (hit != null && hit.transform != null)
                    {
                        // We're over the texture we're drawing on!
                        // Use whatever function the current brush is

                        //current_brush(mouse_world_position);
                        CmdSendPoint(mouse_world_position);
                    }

                    else
                    {
                        // We're not over our destination texture
                        //previous_drag_position = Vector2.zero;
                        Cmd_set_previous_drag_position_to_zero();
                        if (!mouse_was_previously_held_down)
                        {
                            // This is a new drag where the user is left clicking off the canvas
                            // Ensure no drawing happens until a new drag is started
                            no_drawing_on_current_drag = true;
                        }
                    }
                }
                // Mouse is released
                else if (!mouse_held_down)
                {
                    //previous_drag_position = Vector2.zero;
                    Cmd_set_previous_drag_position_to_zero();
                    no_drawing_on_current_drag = false;
                }
                mouse_was_previously_held_down = mouse_held_down;
            }

            //道具效果
            if (anyBuff)
            {
                if (shadingTime > 0)
                {
                    shadingTime -= Time.deltaTime;
                }
                else
                {
                    anyBuff = false;
                    shadingImage.gameObject.SetActive(false);
                }
            }

            //UI
            sysText.text = sysMsg;
            ansText.text = sysAns;
            chatText.text = sysChat;
            scoreText.text = sysScore;
        }

        // Set the colour of pixels in a straight line from start_point all the way to end_point, to ensure everything inbetween is coloured
        public void ColourBetween(Vector2 start_point, Vector2 end_point, int width, Color color)
        {
            // Get the distance from start to finish
            float distance = Vector2.Distance(start_point, end_point);
            Vector2 direction = (start_point - end_point).normalized;

            Vector2 cur_position = start_point;

            // Calculate how many times we should interpolate between start_point and end_point based on the amount of time that has passed since the last update
            float lerp_steps = 1 / distance;

            for (float lerp = 0; lerp <= 1; lerp += lerp_steps)
            {
                cur_position = Vector2.Lerp(start_point, end_point, lerp);
                MarkPixelsToColour(cur_position, width, color);
            }
        }





        public void MarkPixelsToColour(Vector2 center_pixel, int pen_thickness, Color color_of_pen)
        {
            // Figure out how many pixels we need to colour in each direction (x and y)
            int center_x = (int)center_pixel.x;
            int center_y = (int)center_pixel.y;
            int extra_radius = Mathf.Min(0, pen_thickness - 2);

            for (int x = center_x - pen_thickness; x <= center_x + pen_thickness; x++)
            {
                // Check if the X wraps around the image, so we don't draw pixels on the other side of the image
                if (x >= (int)drawable_sprite.rect.width
                    || x < 0)
                    continue;

                for (int y = center_y - pen_thickness; y <= center_y + pen_thickness; y++)
                {
                    MarkPixelToChange(x, y, color_of_pen);
                }
            }
        }
        public void MarkPixelToChange(int x, int y, Color color)
        {
            // Need to transform x and y coordinates to flat coordinates of array
            int array_pos = y * (int)drawable_sprite.rect.width + x;

            // Check if this is a valid position
            if (array_pos > cur_colors.Length || array_pos < 0)
                return;

            cur_colors[array_pos] = color;
        }
        public void ApplyMarkedPixelChanges()
        {
            drawable_texture.SetPixels32(cur_colors);
            drawable_texture.Apply();
        }


        // Directly colours pixels. This method is slower than using MarkPixelsToColour then using ApplyMarkedPixelChanges
        // SetPixels32 is far faster than SetPixel
        // Colours both the center pixel, and a number of pixels around the center pixel based on pen_thickness (pen radius)
        public void ColourPixels(Vector2 center_pixel, int pen_thickness, Color color_of_pen)
        {
            // Figure out how many pixels we need to colour in each direction (x and y)
            int center_x = (int)center_pixel.x;
            int center_y = (int)center_pixel.y;
            int extra_radius = Mathf.Min(0, pen_thickness - 2);

            for (int x = center_x - pen_thickness; x <= center_x + pen_thickness; x++)
            {
                for (int y = center_y - pen_thickness; y <= center_y + pen_thickness; y++)
                {
                    drawable_texture.SetPixel(x, y, color_of_pen);
                }
            }

            drawable_texture.Apply();
        }


        public Vector2 WorldToPixelCoordinates(Vector2 world_position)
        {
            // Change coordinates to local coordinates of this image
            Vector3 local_pos = transform.InverseTransformPoint(world_position);

            // Change these to coordinates of pixels
            float pixelWidth = drawable_sprite.rect.width;
            float pixelHeight = drawable_sprite.rect.height;
            float unitsToPixels = pixelWidth / drawable_sprite.bounds.size.x * transform.localScale.x;

            // Need to center our coordinates
            float centered_x = local_pos.x * unitsToPixels + pixelWidth / 2;
            float centered_y = local_pos.y * unitsToPixels + pixelHeight / 2;

            // Round current mouse position to nearest pixel
            Vector2 pixel_pos = new Vector2(Mathf.RoundToInt(centered_x), Mathf.RoundToInt(centered_y));

            return pixel_pos;
        }


        [Command]
        public void CmdResetCanvas()
        {
            if(process == Process.action)
                RpcResetCanvas();
        }
        // Changes every pixel to be the reset colour
        [ClientRpc]
        public void RpcResetCanvas()
        {
            drawable_texture.SetPixels(clean_colours_array);
            drawable_texture.Apply();
        }



        void Awake()
        {
                drawable = this;
                // DEFAULT BRUSH SET HERE
                current_brush = PenBrush;

                drawable_sprite = this.GetComponent<SpriteRenderer>().sprite;
                drawable_texture = drawable_sprite.texture;

                // Initialize clean pixels to use
                clean_colours_array = new Color[(int)drawable_sprite.rect.width * (int)drawable_sprite.rect.height];
                for (int x = 0; x < clean_colours_array.Length; x++)
                    clean_colours_array[x] = Reset_Colour;

                // Should we reset our canvas image when we hit play in the editor?
                if (Reset_Canvas_On_Play)
                {
                    drawable_texture.SetPixels(clean_colours_array);
                    drawable_texture.Apply();
                }
        }

        void Start()
        {
            if (isServer)
            {
                gm = GameObject.Find("GM").GetComponent<GM>();
                gm.Login(this);
            }
            if (isLocalPlayer)
            {
                btn_clear = GameObject.Find("ClearButton").GetComponent<Button>();
                btn_set_red = GameObject.Find("SetRed").GetComponent<Button>();
                btn_set_Green = GameObject.Find("SetGreen").GetComponent<Button>();
                btn_set_Blue = GameObject.Find("SetBlue").GetComponent<Button>();
                btn_set_Eraser = GameObject.Find("SetEraser").GetComponent<Button>();
                btn_Enter = GameObject.Find("EnterButton").GetComponent<Button>();
                btn_shading[0] = GameObject.Find("ShadingButton1").GetComponent<Button>();
                btn_shading[1] = GameObject.Find("ShadingButton2").GetComponent<Button>();
                btn_shading[2] = GameObject.Find("ShadingButton3").GetComponent<Button>();
                btn_shading[3] = GameObject.Find("ShadingButton4").GetComponent<Button>();
                slider_width = GameObject.Find("WidthSlider").GetComponent<Slider>();
                slider_trans = GameObject.Find("TransparencySlider").GetComponent<Slider>();
                sysText = GameObject.Find("SysText").GetComponent<Text>();
                ansText = GameObject.Find("AnsText").GetComponent<Text>();
                chatText = GameObject.Find("ChatText1").GetComponent<Text>();
                scoreText = GameObject.Find("ScoreText").GetComponent<Text>();
                inputField = GameObject.Find("InputAns").GetComponent<InputField>();
                shadingImage = GameObject.Find("ShadingImage").GetComponent<Image>();
                shadingImage.gameObject.SetActive(false);
                btn_clear.onClick.AddListener(() => CmdResetCanvas());
                btn_set_red.onClick.AddListener(() => CmdSetMarkerRed());
                btn_set_Green.onClick.AddListener(() => CmdSetMarkerGreen());
                btn_set_Blue.onClick.AddListener(() => CmdSetMarkerBlue());
                btn_Enter.onClick.AddListener(() => EterAns());
                btn_Enter.onClick.AddListener(() => ClearEnterField());
                btn_set_Eraser.onClick.AddListener(() => CmdSetEraser());
                btn_shading[0].onClick.AddListener(() => CmdUseShadingItem(0));
                btn_shading[1].onClick.AddListener(() => CmdUseShadingItem(1));
                btn_shading[2].onClick.AddListener(() => CmdUseShadingItem(2));
                btn_shading[3].onClick.AddListener(() => CmdUseShadingItem(3));
                slider_trans.onValueChanged.AddListener(delegate { CmdSetTransparency(slider_trans.value); });
                slider_width.onValueChanged.AddListener(delegate { CmdSetMarkerWidth(slider_width.value); });
                ChatTextAddMsg("測試");
            }
        }
        public void ChatTextAddMsg(string msg)
        {
            chatText.text = chatText.text + "\n" + msg;
        }
        [ClientRpc]
        public void RpcSetPlayer(int sysid)
        {
            if(isLocalPlayer)
                id = sysid-1;
        }
        
        [ClientRpc]
        public void RpcGetPlayerInfo(string name, int id)
        {
            if (isLocalPlayer)
            {
                if (this.id != id)
                {
                    PlayerInfo playerInfo = new PlayerInfo(name, id);
                    playerInfoList.Add(playerInfo);
                }
            }
        }



        //UI
        
        [ClientRpc]
        public void RpcSetUI()
        {
            if (isLocalPlayer)
            {
                btn_shading[id].gameObject.SetActive(false);
                for (int i = id + 1; i < 4; i++)
                {
                    Debug.Log(i);
                    if (i < playersNum)
                        btn_shading[i].transform.position = btn_shading[i].transform.position - new Vector3(40, 0, 0);
                    else
                        btn_shading[i].gameObject.SetActive(false);
                }
            }
        }
        
        void EterAns()
        {
            if(process == Process.wait)
                CmdSendAns(inputField.text, id);
            if (process == Process.creatName)
                CmdSendName(inputField.text, id);
        }

        [Command]
        void CmdSendName(string name, int id)
        {
            gm.GetName(name, id);
        }

        [Command]
        void CmdSendAns(string ans, int id)
        {
            gm.CheckAns(ans, id);
        }

        void ClearEnterField()
        {
            inputField.text = "";
        }

        //打開遮蔽
        [ClientRpc]
        public void RpcOpenShading()
        {
            if(isLocalPlayer)
            {
                Debug.Log("client outside");
                if(!shadingImage.IsActive())
                {
                    shadingImage.gameObject.SetActive(true);
                    shadingImage.sprite = shadingSprite;
                    shadingImage.rectTransform.sizeDelta = new Vector2(400, 300);
                    shadingTime = 3.0f;
                    anyBuff = true;
                    Debug.Log("already open");
                }
            }
        }

        //施放遮蔽
        [Command]
        public void CmdUseShadingItem(int id)
        {
            gm.UseShadingItem(id);
        }
        //setting
        ////////////////////////////////////////////////////////////////////

        // Changing pen settings is easy as changing the static properties Drawable.Pen_Colour and Drawable.Pen_Width
        public void SetMarkerColour(Color new_color)
        {
            Pen_Colour = new_color;
        }
        // new_width is radius in pixels

        [Command]
        public void CmdSetMarkerWidth(float amount)
        {
            if (process == Process.action)
                RpcSetMarkerWidth(amount);
        }

        /*
        public void SetMarkerWidth(int new_width)
        {
            Pen_Width = new_width;
        }
        */

        [ClientRpc]
        public void RpcSetMarkerWidth(float new_width)
        {
            Pen_Width = (int)new_width;
        }

        [Command]
        public void CmdSetTransparency(float amount)
        {
            if (process == Process.action)
                RpcSetTransparency(amount);
        }

        [ClientRpc]
        public void RpcSetTransparency(float amount)
        {
            Transparency = amount;
            Color c = Pen_Colour;
            c.a = amount;
            Pen_Colour = c;
        }


        // Call these these to change the pen settings
        [Command]
        public void CmdSetMarkerRed()
        {
            if (process == Process.action)
            {
                RpcSetMarkerRed();
            }
        }

        [ClientRpc]
        public void RpcSetMarkerRed()
        {
            Color c = Color.red;
            c.a = Transparency;
            SetMarkerColour(c);
            drawable.SetPenBrush();
        }

        [Command]
        public void CmdSetMarkerGreen()
        {
            if (process == Process.action)
                RpcSetMarkerGreen();
        }

        [ClientRpc]
        public void RpcSetMarkerGreen()
        {
            Color c = Color.green;
            c.a = Transparency;
            SetMarkerColour(c);
            drawable.SetPenBrush();
        }

        [Command]
        public void CmdSetMarkerBlue()
        {
            if (process == Process.action)
                RpcSetMarkerBlue();
        }

        [ClientRpc]
        public void RpcSetMarkerBlue()
        {
            Color c = Color.blue;
            c.a = Transparency;
            SetMarkerColour(c);
            drawable.SetPenBrush();
        }

        [Command]
        public void CmdSetEraser()
        {
            if (process == Process.action)
                RpcSetEraser();
        }

        [ClientRpc]
        public void RpcSetEraser()
        {
            //SetMarkerColour(new Color(255f, 255f, 255f, 0f));
            SetMarkerColour(Color.white);
        }

        public void PartialSetEraser()
        {
            SetMarkerColour(new Color(255f, 255f, 255f, 0.5f));
        }
        ///////////////////////////////////////////////////////////////
    }
}